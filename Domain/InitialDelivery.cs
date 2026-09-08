using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum InitialDeliveryTargetKind { Depot, ServiceTrack }
public enum InitialDeliveryState { Available, PlacementPending, ReconcileRequired, Delivered }

[DataContract]
public sealed class InitialDeliveryGrant
{
    [DataMember(Name = "grantId", Order = 1)] public string GrantId { get; set; } = "";
    [DataMember(Name = "sourceCommandId", Order = 2)] public string SourceCommandId { get; set; } = "";
    [DataMember(Name = "owner", Order = 3)] public AssetOwnerRef Owner { get; set; } = new AssetOwnerRef();
    [DataMember(Name = "assetIds", Order = 4)] public List<string> AssetIds { get; set; } = new List<string>();
    [DataMember(Name = "definitionIds", Order = 5)] public List<string> DefinitionIds { get; set; } = new List<string>();
    [DataMember(Name = "freePlacement", Order = 6)] public bool FreePlacement { get; set; } = true;
    [DataMember(Name = "state", Order = 7)] public InitialDeliveryState State { get; set; } = InitialDeliveryState.Available;
    [DataMember(Name = "targetTrackId", Order = 8)] public string? TargetTrackId { get; set; }
    [DataMember(Name = "targetKind", Order = 9)] public InitialDeliveryTargetKind? TargetKind { get; set; }
    [DataMember(Name = "placementCommandId", Order = 10)] public string? PlacementCommandId { get; set; }
    [DataMember(Name = "placementFingerprint", Order = 11)] public string? PlacementFingerprint { get; set; }
    [DataMember(Name = "resultCode", Order = 12)] public string ResultCode { get; set; } = "placement-available";
    [DataMember(Name = "version", Order = 13)] public long Version { get; set; } = 1;
}

[DataContract]
public sealed class InitialDeliveryCommand
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 2)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "grantId", Order = 3)] public string GrantId { get; set; } = "";
    [DataMember(Name = "targetTrackId", Order = 4)] public string TargetTrackId { get; set; } = "";
    [DataMember(Name = "targetKind", Order = 5)] public InitialDeliveryTargetKind TargetKind { get; set; }
    [DataMember(Name = "expectedGrantVersion", Order = 6)] public long ExpectedGrantVersion { get; set; }
}

public sealed class InitialDeliveryPortResult
{
    public WorldOwnershipOutcome Outcome { get; set; }
    public IReadOnlyList<string> PersistentCarGuids { get; set; } = Array.Empty<string>();
    public string Detail { get; set; } = "";
}

public interface IInitialDeliveryPort
{
    InitialDeliveryPortResult Preflight(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds);
    InitialDeliveryPortResult Place(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds);
    InitialDeliveryPortResult Inspect(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds);
}

public interface IInitialDeliveryCheckpointPort
{
    bool TryCheckpoint(InitialDeliveryGrant grant, string phase);
}

public sealed class NoOpInitialDeliveryCheckpointPort : IInitialDeliveryCheckpointPort
{
    public bool TryCheckpoint(InitialDeliveryGrant grant, string phase) => true;
}

public sealed class DisabledInitialDeliveryPort : IInitialDeliveryPort
{
    private static InitialDeliveryPortResult Disabled() => new InitialDeliveryPortResult { Outcome = WorldOwnershipOutcome.NotApplied, Detail = "initial-delivery-adapter-disabled" };
    public InitialDeliveryPortResult Preflight(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds) => Disabled();
    public InitialDeliveryPortResult Place(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds) => Disabled();
    public InitialDeliveryPortResult Inspect(string operationId, string trackId, InitialDeliveryTargetKind targetKind, IReadOnlyList<string> definitionIds) => Disabled();
}

public sealed class InitialDeliveryEngine
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    private readonly IInitialDeliveryPort port;
    private readonly IInitialDeliveryCheckpointPort checkpoint;

    public InitialDeliveryEngine(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority, IInitialDeliveryPort port,
        IInitialDeliveryCheckpointPort? checkpoint = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
        this.checkpoint = checkpoint ?? new NoOpInitialDeliveryCheckpointPort();
        VehicleAcquisitionPersistence.Validate(state);
    }

    public InitialDeliveryGrant Place(InitialDeliveryCommand command)
    {
        lock (gate)
        {
            RequireHost(); Require(command);
            var grant = state.InitialDeliveries.SingleOrDefault(x => x.GrantId == command.GrantId) ?? throw new InvalidOperationException("Unknown initial delivery grant.");
            var fingerprint = Fingerprint(command);
            if (grant.PlacementCommandId == command.CommandId)
            {
                if (!string.Equals(grant.PlacementFingerprint, fingerprint, StringComparison.Ordinal)) throw new InvalidOperationException("Initial delivery command ID payload conflict.");
                return grant;
            }
            if (grant.State != InitialDeliveryState.Available) throw new InvalidOperationException("Initial delivery grant is not available.");
            if (grant.Version != command.ExpectedGrantVersion) throw new InvalidOperationException("Initial delivery grant version mismatch.");
            Authorize(grant, command.RequesterId);
            foreach (var assetId in grant.AssetIds)
                if (state.Ownership.Single(x => x.AssetId == assetId).Owner.Key != grant.Owner.Key) throw new InvalidOperationException("Initial delivery ownership changed.");

            grant.PlacementCommandId = command.CommandId;
            grant.PlacementFingerprint = fingerprint;
            grant.TargetTrackId = command.TargetTrackId.Trim();
            grant.TargetKind = command.TargetKind;
            grant.ResultCode = "placement-preflight";
            grant.Version++;
            var preflight = port.Preflight(command.CommandId + ":preflight", grant.TargetTrackId, command.TargetKind, grant.DefinitionIds);
            if (preflight.Outcome != WorldOwnershipOutcome.Applied)
            {
                grant.ResultCode = "placement-refused:" + SafeDetail(preflight.Detail);
                return grant;
            }

            grant.State = InitialDeliveryState.PlacementPending;
            grant.ResultCode = "placement-pending";
            grant.Version++;
            if (!TryCheckpoint(grant, "placement-pending"))
            {
                grant.State = InitialDeliveryState.ReconcileRequired;
                grant.ResultCode = "placement-reconcile:checkpoint-pending-failed";
                grant.Version++;
                return grant;
            }
            InitialDeliveryPortResult result;
            try { result = port.Place(command.CommandId + ":spawn", grant.TargetTrackId, command.TargetKind, grant.DefinitionIds); }
            catch (Exception exception)
            {
                grant.State = InitialDeliveryState.ReconcileRequired;
                grant.ResultCode = "placement-exception:" + exception.GetType().Name;
                grant.Version++;
                return CheckpointResult(grant, "placement-exception");
            }
            var resolved = Resolve(grant, result);
            return CheckpointResult(resolved, "placement-result");
        }
    }

    public InitialDeliveryGrant Reconcile(string grantId)
    {
        lock (gate)
        {
            RequireHost();
            var grant = state.InitialDeliveries.Single(x => x.GrantId == grantId);
            if (grant.State == InitialDeliveryState.Delivered || grant.State == InitialDeliveryState.Available) return grant;
            if (string.IsNullOrWhiteSpace(grant.PlacementCommandId) || string.IsNullOrWhiteSpace(grant.TargetTrackId) || !grant.TargetKind.HasValue) throw new InvalidOperationException("Pending delivery has no complete placement identity.");
            var result = port.Inspect(grant.PlacementCommandId + ":spawn", grant.TargetTrackId!, grant.TargetKind.Value, grant.DefinitionIds);
            return CheckpointResult(Resolve(grant, result), "placement-reconciled");
        }
    }

    public InitialDeliveryGrant GrantStarterBundle(string commandId, string playerId, IReadOnlyList<string> definitionIds)
    {
        lock (gate)
        {
            RequireHost();
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(playerId) || definitionIds == null || definitionIds.Count == 0 || definitionIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("A complete starter bundle grant is required.");
            var known = state.InitialDeliveries.Where(x => x.SourceCommandId == commandId).OrderBy(x => x.GrantId, StringComparer.Ordinal).ToArray();
            if (known.Length > 0)
            {
                if (known.Any(x => x.Owner.Key != AssetOwnerRef.Player(playerId).Key) || !known.SelectMany(x => x.DefinitionIds).SequenceEqual(definitionIds)) throw new InvalidOperationException("Starter grant command ID payload conflict.");
                return known[0];
            }
            if (state.Economy.History.Any(x => x.Kind == "starter-bundle-granted" && x.ActorIds.Contains(playerId))) throw new InvalidOperationException("Starter bundle was already granted to this player identity.");
            var owner = AssetOwnerRef.Player(playerId);
            var assets = definitionIds.Select(definitionId => CreateVirtualAsset(definitionId, owner, "starter-bundle")).ToArray();
            if (assets.Length > 1) state.Assets.Bundles.Add(new AssetBundle { BundleId = Guid.NewGuid().ToString("N"), ComponentAssetIds = assets.Select(x => x.AssetId).ToList() });
            // Each starter vehicle is delivered independently. This keeps radio placement usable on
            // short service tracks and avoids requiring an empty track long enough for the whole consist.
            var grants = assets.Select((asset, index) => CreateGrant(commandId + ":initial-delivery:" + index, commandId, owner, new[] { asset })).ToArray();
            state.InitialDeliveries.AddRange(grants);
            state.Economy.History.Add(new EconomicHistoryRecord { EventId = commandId, Kind = "starter-bundle-granted", ActorIds = new List<string> { playerId }, Fingerprint = string.Join("|", playerId, string.Join(",", definitionIds)) });
            return grants[0];
        }
    }

    internal static InitialDeliveryGrant CreateGrant(string grantId, string sourceCommandId, AssetOwnerRef owner, IReadOnlyList<FleetAsset> assets)
    {
        var grant = new InitialDeliveryGrant { GrantId = grantId, SourceCommandId = sourceCommandId, Owner = Clone(owner), AssetIds = assets.Select(x => x.AssetId).ToList(), DefinitionIds = assets.Select(x => x.DefinitionId).ToList() };
        return grant;
    }

    private FleetAsset CreateVirtualAsset(string definitionId, AssetOwnerRef owner, string origin)
    {
        if (!state.Assets.Definitions.Any(x => x.DefinitionId == definitionId)) state.Assets.Definitions.Add(new AssetDefinition { DefinitionId = definitionId, Origin = origin });
        var asset = FleetAsset.Create(definitionId, Guid.NewGuid().ToString("D"));
        asset.GameLink.State = PersistentLinkState.TemporarilyAbsent;
        asset.GameLink.Detail = "Owned virtual stock awaiting authorized initial delivery.";
        state.Assets.Assets.Add(asset);
        state.Ownership.Add(new AssetOwnership { AssetId = asset.AssetId, Owner = Clone(owner), Version = 1 });
        FleetManagementEngine.EnsureAsset(state, asset.AssetId, null, definitionId, definitionId);
        return asset;
    }

    private InitialDeliveryGrant Resolve(InitialDeliveryGrant grant, InitialDeliveryPortResult result)
    {
        if (result.Outcome == WorldOwnershipOutcome.NotApplied)
        {
            grant.State = InitialDeliveryState.Available;
            grant.ResultCode = "placement-refused:" + SafeDetail(result.Detail);
            grant.Version++;
            return grant;
        }
        if (result.Outcome == WorldOwnershipOutcome.Unknown)
        {
            grant.State = InitialDeliveryState.ReconcileRequired;
            grant.ResultCode = "placement-reconcile:" + SafeDetail(result.Detail);
            grant.Version++;
            return grant;
        }
        var guids = result.PersistentCarGuids ?? Array.Empty<string>();
        if (guids.Count != grant.AssetIds.Count || guids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != guids.Count || guids.Any(x => !Guid.TryParse(x, out var parsed) || parsed == Guid.Empty))
        {
            grant.State = InitialDeliveryState.ReconcileRequired;
            grant.ResultCode = "placement-reconcile:incomplete-or-invalid-component-set";
            grant.Version++;
            return grant;
        }
        for (var i = 0; i < grant.AssetIds.Count; i++)
        {
            var asset = state.Assets.Assets.Single(x => x.AssetId == grant.AssetIds[i]);
            asset.GameLink.Value = Guid.Parse(guids[i]).ToString("D");
            asset.GameLink.ExpectedDefinitionId = grant.DefinitionIds[i];
            asset.GameLink.State = PersistentLinkState.Resolved;
            asset.GameLink.Detail = "Initial delivery confirmed by authoritative adapter.";
            var fleet = state.Fleet.Single(x => x.AssetId == asset.AssetId);
            fleet.LastKnownLocation = grant.TargetTrackId;
            fleet.OperationalState = FleetOperationalState.Available;
            fleet.Version++;
        }
        grant.State = InitialDeliveryState.Delivered;
        grant.ResultCode = "initial-delivery-complete";
        grant.Version++;
        return grant;
    }

    private InitialDeliveryGrant CheckpointResult(InitialDeliveryGrant grant, string phase)
    {
        if (TryCheckpoint(grant, phase)) return grant;
        if (grant.State != InitialDeliveryState.Available)
        {
            grant.State = InitialDeliveryState.ReconcileRequired;
            grant.ResultCode = "placement-reconcile:checkpoint-result-failed";
            grant.Version++;
        }
        return grant;
    }

    private bool TryCheckpoint(InitialDeliveryGrant grant, string phase)
    {
        try { return checkpoint.TryCheckpoint(grant, phase); }
        catch { return false; }
    }

    private void Authorize(InitialDeliveryGrant grant, string requester)
    {
        var player = state.Economy.Players.SingleOrDefault(x => x.PlayerId == requester) ?? throw new InvalidOperationException("Unknown requester.");
        if (grant.Owner.Kind == AssetOwnerKind.Player)
        {
            if (grant.Owner.OwnerId != player.PlayerId) throw new UnauthorizedAccessException("Only the owner can place personal rolling stock.");
            return;
        }
        if (grant.Owner.Kind != AssetOwnerKind.Company || player.CompanyId != grant.Owner.OwnerId) throw new UnauthorizedAccessException("Requester does not belong to the owning company.");
        var company = state.Economy.Companies.Single(x => x.CompanyId == grant.Owner.OwnerId);
        if (company.Liquidating || (company.LeaderId != requester && (!company.DelegatedPermissions.TryGetValue(requester, out var rights) || !rights.Contains(CompanyPermission.ManageFleet)))) throw new UnauthorizedAccessException("ManageFleet permission is required.");
    }

    private void RequireHost() { if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out var reason)) throw new InvalidOperationException(reason); }
    private static void Require(InitialDeliveryCommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.CommandId) || string.IsNullOrWhiteSpace(command.RequesterId) || string.IsNullOrWhiteSpace(command.GrantId) || string.IsNullOrWhiteSpace(command.TargetTrackId)) throw new ArgumentException("A complete initial delivery command is required.");
        if (command.TargetTrackId.Length > 128) throw new ArgumentException("Delivery track ID is too long.");
        if (!Enum.IsDefined(typeof(InitialDeliveryTargetKind), command.TargetKind)) throw new ArgumentException("Delivery target must be a depot or service track.");
    }
    private static string Fingerprint(InitialDeliveryCommand command) => string.Join("|", command.RequesterId, command.GrantId, command.TargetTrackId.Trim(), command.TargetKind, command.ExpectedGrantVersion);
    private static string SafeDetail(string value) => string.IsNullOrWhiteSpace(value) ? "unspecified" : value.Trim();
    private static AssetOwnerRef Clone(AssetOwnerRef value) => new AssetOwnerRef { Kind = value.Kind, OwnerId = value.OwnerId };
}

public static class InitialDeliveryValidation
{
    public static void Validate(VehicleAcquisitionSnapshot state)
    {
        var grants = state.InitialDeliveries ?? new List<InitialDeliveryGrant>();
        if (grants.GroupBy(x => x.GrantId).Any(x => x.Count() != 1)) throw new InvalidOperationException("Duplicate initial delivery grant identity.");
        foreach (var grant in grants)
        {
            if (grant == null || string.IsNullOrWhiteSpace(grant.GrantId) || string.IsNullOrWhiteSpace(grant.SourceCommandId) || grant.Owner == null || grant.AssetIds == null || grant.DefinitionIds == null || grant.AssetIds.Count == 0 || grant.AssetIds.Count != grant.DefinitionIds.Count || grant.AssetIds.Distinct(StringComparer.Ordinal).Count() != grant.AssetIds.Count || grant.AssetIds.Any(id => !state.Assets.Assets.Any(a => a.AssetId == id)) || grant.DefinitionIds.Any(string.IsNullOrWhiteSpace) || grant.Version < 1) throw new InvalidOperationException("Invalid initial delivery grant.");
            if (grant.State == InitialDeliveryState.Delivered && grant.AssetIds.Any(id => state.Assets.Assets.Single(a => a.AssetId == id).GameLink.State != PersistentLinkState.Resolved)) throw new InvalidOperationException("Delivered grant contains an unresolved asset.");
        }
    }
}
