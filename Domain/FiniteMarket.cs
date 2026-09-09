using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum MarketListingKind { ExistingAsset, NewOrder }
public enum MarketListingState { Available, Reserved, DeliveryPending, Sold, Expired }
public enum MarketPurchaseState { Succeeded, Rejected, Compensated, ReconcileRequired }

[DataContract]
public sealed class MarketCatalogEntry
{
    [DataMember(Name = "definitionId", Order = 1)] public string DefinitionId { get; set; } = "";
    [DataMember(Name = "categoryId", Order = 2)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "basePrice", Order = 3)] public long BasePrice { get; set; }
    [DataMember(Name = "minimumMarketFactor", Order = 4)] public decimal MinimumMarketFactor { get; set; } = 0.8m;
    [DataMember(Name = "maximumMarketFactor", Order = 5)] public decimal MaximumMarketFactor { get; set; } = 1.2m;
    [DataMember(Name = "minimumConditionFactor", Order = 6)] public decimal MinimumConditionFactor { get; set; } = 0.4m;
    [DataMember(Name = "transferFee", Order = 7)] public long TransferFee { get; set; }
    [DataMember(Name = "buybackRate", Order = 8)] public decimal BuybackRate { get; set; } = 0.5m;
    [DataMember(Name = "quoteDurationTicks", Order = 9)] public long QuoteDurationTicks { get; set; } = 100;
    [DataMember(Name = "version", Order = 10)] public long Version { get; set; } = 1;
}

[DataContract]
public sealed class MarketStockEntry
{
    [DataMember(Name = "locationId", Order = 1)] public string LocationId { get; set; } = "";
    [DataMember(Name = "definitionId", Order = 2)] public string DefinitionId { get; set; } = "";
    [DataMember(Name = "available", Order = 3)] public int Available { get; set; }
    [DataMember(Name = "capacity", Order = 4)] public int Capacity { get; set; }
    [DataMember(Name = "version", Order = 5)] public long Version { get; set; }
    public string Key => LocationId + ":" + DefinitionId;
}

[DataContract]
public sealed class MarketListing
{
    [DataMember(Name = "listingId", Order = 1)] public string ListingId { get; set; } = "";
    [DataMember(Name = "kind", Order = 2)] public MarketListingKind Kind { get; set; }
    [DataMember(Name = "assetId", Order = 3)] public string? AssetId { get; set; }
    [DataMember(Name = "definitionId", Order = 4)] public string DefinitionId { get; set; } = "";
    [DataMember(Name = "categoryId", Order = 5)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "locationId", Order = 6)] public string LocationId { get; set; } = "";
    [DataMember(Name = "price", Order = 7)] public long Price { get; set; }
    [DataMember(Name = "referenceValue", Order = 8)] public long ReferenceValue { get; set; }
    [DataMember(Name = "condition", Order = 9)] public decimal Condition { get; set; }
    [DataMember(Name = "marketFactor", Order = 10)] public decimal MarketFactor { get; set; }
    [DataMember(Name = "transferFee", Order = 11)] public long TransferFee { get; set; }
    [DataMember(Name = "createdTick", Order = 12)] public long CreatedTick { get; set; }
    [DataMember(Name = "expiresTick", Order = 13)] public long ExpiresTick { get; set; }
    [DataMember(Name = "state", Order = 14)] public MarketListingState State { get; set; }
    [DataMember(Name = "reservedBy", Order = 15)] public string? ReservedBy { get; set; }
    [DataMember(Name = "version", Order = 16)] public long Version { get; set; }
}

[DataContract]
public sealed class MarketPurchaseCommand
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 2)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "listingId", Order = 3)] public string ListingId { get; set; } = "";
    [DataMember(Name = "buyer", Order = 4)] public AssetOwnerRef Buyer { get; set; } = new AssetOwnerRef();
    [DataMember(Name = "payer", Order = 5)] public AccountRef Payer { get; set; } = new AccountRef();
    [DataMember(Name = "expectedListingVersion", Order = 6)] public long ExpectedListingVersion { get; set; }
    [DataMember(Name = "expectedWalletVersion", Order = 7)] public long ExpectedWalletVersion { get; set; }
    [DataMember(Name = "expectedPlayerVersion", Order = 8)] public long ExpectedPlayerVersion { get; set; }
    [DataMember(Name = "expectedCompanyVersion", Order = 9)] public long? ExpectedCompanyVersion { get; set; }
}

[DataContract]
public sealed class MarketPurchaseRecord
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "fingerprint", Order = 2)] public string Fingerprint { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 3)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "listingId", Order = 4)] public string ListingId { get; set; } = "";
    [DataMember(Name = "assetId", Order = 5)] public string? AssetId { get; set; }
    [DataMember(Name = "buyer", Order = 6)] public AssetOwnerRef Buyer { get; set; } = new AssetOwnerRef();
    [DataMember(Name = "payer", Order = 7)] public AccountRef Payer { get; set; } = new AccountRef();
    [DataMember(Name = "price", Order = 8)] public long Price { get; set; }
    [DataMember(Name = "state", Order = 9)] public MarketPurchaseState State { get; set; }
    [DataMember(Name = "resultCode", Order = 10)] public string ResultCode { get; set; } = "";
    [DataMember(Name = "deliveryOperationId", Order = 11)] public string DeliveryOperationId { get; set; } = "";
    [DataMember(Name = "debited", Order = 12)] public bool Debited { get; set; }
    [DataMember(Name = "ownershipCommitted", Order = 13)] public bool OwnershipCommitted { get; set; }
}

[DataContract]
public sealed class FiniteMarketState
{
    public const string CurrentSchema = "bdvm.finite-market";
    public const int CurrentVersion = 1;
    [DataMember(Name = "schema", Order = 1)] public string Schema { get; set; } = CurrentSchema;
    [DataMember(Name = "schemaVersion", Order = 2)] public int SchemaVersion { get; set; } = CurrentVersion;
    [DataMember(Name = "clockTick", Order = 3)] public long ClockTick { get; set; }
    [DataMember(Name = "randomState", Order = 4)] public ulong RandomState { get; set; } = 0x9E3779B97F4A7C15UL;
    [DataMember(Name = "catalog", Order = 5)] public List<MarketCatalogEntry> Catalog { get; set; } = new List<MarketCatalogEntry>();
    [DataMember(Name = "stock", Order = 6)] public List<MarketStockEntry> Stock { get; set; } = new List<MarketStockEntry>();
    [DataMember(Name = "listings", Order = 7)] public List<MarketListing> Listings { get; set; } = new List<MarketListing>();
    [DataMember(Name = "purchases", Order = 8)] public List<MarketPurchaseRecord> Purchases { get; set; } = new List<MarketPurchaseRecord>();
}

public sealed class MarketDeliveryResult
{
    public WorldOwnershipOutcome Outcome { get; set; }
    public string? PersistentCarGuid { get; set; }
    public string Detail { get; set; } = "";
}

public interface IMarketDeliveryPort
{
    MarketDeliveryResult Deliver(string operationId, string definitionId, string locationId);
    MarketDeliveryResult Inspect(string operationId, string definitionId, string locationId);
}

public interface IMarketPurchaseCheckpointPort
{
    bool TryCheckpoint(MarketPurchaseRecord purchase, string phase);
}

public sealed class NoOpMarketPurchaseCheckpointPort : IMarketPurchaseCheckpointPort
{
    public bool TryCheckpoint(MarketPurchaseRecord purchase, string phase) => true;
}

public sealed class DisabledMarketDeliveryPort : IMarketDeliveryPort
{
    public MarketDeliveryResult Deliver(string operationId, string definitionId, string locationId) => new MarketDeliveryResult { Outcome = WorldOwnershipOutcome.NotApplied, Detail = "new-vehicle-delivery-adapter-disabled" };
    public MarketDeliveryResult Inspect(string operationId, string definitionId, string locationId) => new MarketDeliveryResult { Outcome = WorldOwnershipOutcome.NotApplied, Detail = "new-vehicle-delivery-adapter-disabled" };
}

public sealed class FiniteMarketEngine
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    private readonly IExistingVehicleOwnershipAdapter ownershipWorld;
    private readonly IMarketDeliveryPort delivery;
    private readonly IMarketPurchaseCheckpointPort checkpoint;

    public FiniteMarketEngine(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority, IExistingVehicleOwnershipAdapter ownershipWorld, IMarketDeliveryPort delivery,
        IMarketPurchaseCheckpointPort? checkpoint = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state)); this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        this.ownershipWorld = ownershipWorld ?? throw new ArgumentNullException(nameof(ownershipWorld)); this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        this.checkpoint = checkpoint ?? new NoOpMarketPurchaseCheckpointPort();
        VehicleAcquisitionPersistence.Validate(state);
    }

    public MarketListing PublishExisting(string listingId, string assetId, string locationId, decimal condition, decimal requestedMarketFactor)
    {
        lock (gate)
        {
            RequireHost(); RequireIdentity(listingId, locationId);
            var known = state.Market.Listings.SingleOrDefault(x => x.ListingId == listingId); if (known != null) return known;
            var asset = state.Assets.Assets.Single(x => x.AssetId == assetId);
            if (state.Ownership.Single(x => x.AssetId == assetId).Owner.Kind != AssetOwnerKind.Merchant) throw new InvalidOperationException("Only a merchant-owned asset can be listed.");
            if (state.Market.Listings.Any(x => x.AssetId == assetId && (x.State == MarketListingState.Available || x.State == MarketListingState.Reserved || x.State == MarketListingState.DeliveryPending))) throw new InvalidOperationException("Asset already has an active listing.");
            var catalog = Catalog(asset.DefinitionId); var factor = Clamp(requestedMarketFactor, catalog.MinimumMarketFactor, catalog.MaximumMarketFactor);
            FleetManagementEngine.EnsureAsset(state, asset.AssetId, catalog.CategoryId, asset.DefinitionId, asset.DefinitionId);
            var listing = Listing(listingId, MarketListingKind.ExistingAsset, assetId, catalog, locationId, condition, factor);
            state.Market.Listings.Add(listing); return listing;
        }
    }

    public VehicleResaleQuote PrepareBuybackQuote(string quoteId, string requesterId, string assetId, decimal condition,
        decimal requestedMarketFactor, long fuelValue, IAssetReleaseGuard releaseGuard)
    {
        lock (gate)
        {
            RequireHost();
            var asset = state.Assets.Assets.Single(x => x.AssetId == assetId);
            var catalog = Catalog(asset.DefinitionId);
            var factor = Clamp(requestedMarketFactor, catalog.MinimumMarketFactor, catalog.MaximumMarketFactor);
            var reference = ConditionReference(catalog, condition);
            var gross = checked(decimal.ToInt64(decimal.Floor(reference * factor * catalog.BuybackRate)));
            var proceeds = Math.Max(0, checked(gross + fuelValue - catalog.TransferFee));
            return new VehicleResaleEngine(state, authority, releaseGuard, ownershipWorld).PrepareQuote(
                quoteId, requesterId, assetId, proceeds, reference, ReferenceValueSource.DynamicMarket,
                condition, fuelValue, catalog.TransferFee);
        }
    }

    public VehicleResaleQuote PrepareBundleBuybackQuote(string quoteId, string requesterId, string bundleId,
        IReadOnlyDictionary<string, decimal> conditions, decimal requestedMarketFactor, long fuelValue,
        IAssetReleaseGuard releaseGuard)
    {
        lock (gate)
        {
            RequireHost();
            var bundle = state.Assets.Bundles.Single(x => x.BundleId == bundleId);
            long reference = 0; long gross = 0; long fees = 0; decimal conditionTotal = 0m;
            foreach (var assetId in bundle.ComponentAssetIds)
            {
                var asset = state.Assets.Assets.Single(x => x.AssetId == assetId);
                var catalog = Catalog(asset.DefinitionId);
                var condition = conditions.TryGetValue(assetId, out var supplied) ? supplied : throw new InvalidOperationException("Every bundle component requires an observed condition.");
                var factor = Clamp(requestedMarketFactor, catalog.MinimumMarketFactor, catalog.MaximumMarketFactor);
                var componentReference = ConditionReference(catalog, condition);
                reference = checked(reference + componentReference);
                gross = checked(gross + decimal.ToInt64(decimal.Floor(componentReference * factor * catalog.BuybackRate)));
                fees = checked(fees + catalog.TransferFee); conditionTotal += condition;
            }
            var proceeds = Math.Max(0, checked(gross + fuelValue - fees));
            var averageCondition = bundle.ComponentAssetIds.Count == 0 ? 1m : conditionTotal / bundle.ComponentAssetIds.Count;
            return new VehicleResaleEngine(state, authority, releaseGuard, ownershipWorld).PrepareBundleQuote(
                quoteId, requesterId, bundleId, proceeds, reference, ReferenceValueSource.DynamicMarket,
                averageCondition, fuelValue, fees);
        }
    }

    public MarketListing GenerateNewOrder(string listingId, string definitionId, string locationId)
    {
        lock (gate)
        {
            RequireHost(); RequireIdentity(listingId, locationId);
            var known = state.Market.Listings.SingleOrDefault(x => x.ListingId == listingId); if (known != null) return known;
            var catalog = Catalog(definitionId); var stock = state.Market.Stock.Single(x => x.DefinitionId == definitionId && x.LocationId == locationId);
            if (stock.Available <= 0) throw new InvalidOperationException("Finite market stock is exhausted.");
            var dynamicMetric = state.DynamicEconomy.Metrics.SingleOrDefault(x => x.CategoryId == catalog.CategoryId);
            var factor = dynamicMetric == null ? NextFactor(catalog.MinimumMarketFactor, catalog.MaximumMarketFactor) : Clamp(dynamicMetric.SmoothedFactor, catalog.MinimumMarketFactor, catalog.MaximumMarketFactor);
            var listing = Listing(listingId, MarketListingKind.NewOrder, null, catalog, locationId, 1m, factor);
            stock.Available--; stock.Version++; state.Market.Listings.Add(listing); return listing;
        }
    }

    public void AdvanceTo(long tick)
    {
        lock (gate)
        {
            RequireHost(); if (tick < state.Market.ClockTick) throw new InvalidOperationException("Market clock cannot move backwards.");
            state.Market.ClockTick = tick;
            foreach (var listing in state.Market.Listings.Where(x => x.State == MarketListingState.Available && x.ExpiresTick <= tick).ToArray())
            {
                listing.State = MarketListingState.Expired; listing.Version++;
                if (listing.Kind == MarketListingKind.NewOrder)
                {
                    var stock = state.Market.Stock.Single(x => x.LocationId == listing.LocationId && x.DefinitionId == listing.DefinitionId);
                    if (stock.Available < stock.Capacity) { stock.Available++; stock.Version++; }
                }
            }
        }
    }

    public MarketPurchaseRecord Purchase(MarketPurchaseCommand command)
    {
        lock (gate)
        {
            RequireHost(); Require(command);
            var fingerprint = Fingerprint(command); var known = state.Market.Purchases.SingleOrDefault(x => x.CommandId == command.CommandId);
            if (known != null) { if (known.Fingerprint != fingerprint) throw new InvalidOperationException("Market command ID payload conflict."); return known; }
            var listing = state.Market.Listings.SingleOrDefault(x => x.ListingId == command.ListingId);
            var record = NewRecord(command, listing, fingerprint); state.Market.Purchases.Add(record);
            var refusal = Validate(command, listing); if (refusal != null) return Reject(record, refusal);
            listing!.State = MarketListingState.Reserved; listing.ReservedBy = command.CommandId; listing.Version++;
            var wallet = state.Economy.Wallets.Single(x => x.Account.Key == command.Payer.Key); wallet.Balance -= listing.Price; wallet.Version++; record.Debited = true;
            if (!state.Economy.Ledger.Any(x => x.EntryId == command.CommandId + ":market-debit")) state.Economy.Ledger.Add(new LedgerEntry { EntryId = command.CommandId + ":market-debit", CommandId = command.CommandId, Kind = LedgerEntryKind.VehiclePurchase, Debit = wallet.Account, Amount = listing.Price, Detail = "finite-market;listing=" + listing.ListingId + ";location=" + listing.LocationId + ";factor=" + listing.MarketFactor + ";fee=" + listing.TransferFee });
            record.DeliveryOperationId = command.CommandId + (listing.Kind == MarketListingKind.ExistingAsset ? ":ownership" : ":initial-delivery");
            record.State = MarketPurchaseState.ReconcileRequired;
            record.ResultCode = "market-purchase-pending";
            if (!TryCheckpoint(record, "purchase-pending")) return Compensate(record, listing, wallet, "checkpoint-pending-failed");
            if (listing.Kind == MarketListingKind.ExistingAsset)
            {
                var asset = state.Assets.Assets.Single(x => x.AssetId == listing.AssetId);
                var outcome = ownershipWorld.ApplyOwner(record.DeliveryOperationId, asset.GameLink.Value!, command.Buyer);
                if (outcome == WorldOwnershipOutcome.NotApplied) return CheckpointResult(Compensate(record, listing, wallet, "world-owner-not-applied"), "purchase-compensated");
                if (outcome == WorldOwnershipOutcome.Unknown) { listing.State = MarketListingState.DeliveryPending; return CheckpointResult(Pending(record, "world-owner-unknown"), "purchase-world-unknown"); }
                return CheckpointResult(Commit(record, listing, listing.AssetId!), "purchase-complete");
            }
            return CheckpointResult(CommitVirtualNew(record, listing), "purchase-complete");
        }
    }

    public MarketPurchaseRecord Reconcile(string commandId)
    {
        lock (gate)
        {
            RequireHost();
            var record = state.Market.Purchases.Single(x => x.CommandId == commandId); if (record.State != MarketPurchaseState.ReconcileRequired) return record;
            var listing = state.Market.Listings.Single(x => x.ListingId == record.ListingId);
            if (listing.Kind == MarketListingKind.ExistingAsset)
            {
                var asset = state.Assets.Assets.Single(x => x.AssetId == listing.AssetId);
                var outcome = ownershipWorld.InspectOwner(asset.GameLink.Value!, record.Buyer);
                if (outcome == WorldOwnershipOutcome.NotApplied)
                {
                    var wallet = state.Economy.Wallets.Single(x => x.Account.Key == record.Payer.Key);
                    return CheckpointResult(Compensate(record, listing, wallet, "world-owner-not-applied"), "purchase-compensated");
                }
                if (outcome != WorldOwnershipOutcome.Applied) return CheckpointResult(Pending(record, "world-owner-still-unknown"), "purchase-world-unknown");
                return CheckpointResult(Commit(record, listing, listing.AssetId!), "purchase-reconciled");
            }
            if (record.OwnershipCommitted && !string.IsNullOrWhiteSpace(record.AssetId))
                return CheckpointResult(Commit(record, listing, record.AssetId!), "purchase-reconciled");
            var delivered = delivery.Inspect(record.DeliveryOperationId, listing.DefinitionId, listing.LocationId);
            if (delivered.Outcome == WorldOwnershipOutcome.NotApplied)
            {
                var wallet = state.Economy.Wallets.Single(x => x.Account.Key == record.Payer.Key);
                return CheckpointResult(Compensate(record, listing, wallet, "delivery-not-applied"), "purchase-compensated");
            }
            if (delivered.Outcome != WorldOwnershipOutcome.Applied || !ValidGuid(delivered.PersistentCarGuid)) return CheckpointResult(Pending(record, "delivery-still-unknown"), "purchase-world-unknown");
            return CheckpointResult(CommitNew(record, listing, delivered.PersistentCarGuid!), "purchase-reconciled");
        }
    }

    private MarketPurchaseRecord CommitNew(MarketPurchaseRecord record, MarketListing listing, string carGuid)
    {
        var existing = state.Assets.Assets.SingleOrDefault(x => string.Equals(x.GameLink.Value, carGuid, StringComparison.OrdinalIgnoreCase));
        if (existing != null && existing.AssetId != listing.AssetId) return Pending(record, "delivered-guid-already-registered");
        if (!state.Assets.Definitions.Any(x => x.DefinitionId == listing.DefinitionId)) state.Assets.Definitions.Add(new AssetDefinition { DefinitionId = listing.DefinitionId, Origin = "finite-market-delivery" });
        var asset = existing ?? FleetAsset.Create(listing.DefinitionId, carGuid); asset.GameLink.State = PersistentLinkState.Resolved;
        if (existing == null) { state.Assets.Assets.Add(asset); state.Ownership.Add(new AssetOwnership { AssetId = asset.AssetId, Owner = AssetOwnerRef.Merchant("runtime-market") }); }
        listing.AssetId = asset.AssetId; record.AssetId = asset.AssetId;
        FleetManagementEngine.EnsureAsset(state, asset.AssetId, listing.CategoryId, listing.DefinitionId, listing.DefinitionId);
        return Commit(record, listing, asset.AssetId);
    }

    private MarketPurchaseRecord CommitVirtualNew(MarketPurchaseRecord record, MarketListing listing)
    {
        if (!state.Assets.Definitions.Any(x => x.DefinitionId == listing.DefinitionId))
            state.Assets.Definitions.Add(new AssetDefinition { DefinitionId = listing.DefinitionId, Origin = "finite-market-catalog" });
        var asset = FleetAsset.Create(listing.DefinitionId, Guid.NewGuid().ToString("D"));
        asset.GameLink.State = PersistentLinkState.TemporarilyAbsent;
        asset.GameLink.Detail = "Purchased virtual stock awaiting authorized initial delivery.";
        state.Assets.Assets.Add(asset);
        state.Ownership.Add(new AssetOwnership { AssetId = asset.AssetId, Owner = Clone(record.Buyer), Version = 1 });
        var fleet = FleetManagementEngine.EnsureAsset(state, asset.AssetId, listing.CategoryId, listing.DefinitionId, listing.DefinitionId);
        fleet.OperationalState = FleetOperationalState.Stored;
        fleet.LastKnownLocation = "virtual:" + listing.LocationId;
        fleet.Version++;
        listing.AssetId = asset.AssetId;
        record.AssetId = asset.AssetId;
        record.OwnershipCommitted = true;
        record.DeliveryOperationId = record.CommandId + ":initial-delivery";
        state.InitialDeliveries.Add(InitialDeliveryEngine.CreateGrant(record.DeliveryOperationId, record.CommandId, record.Buyer, new[] { asset }));
        listing.State = MarketListingState.Sold;
        listing.ReservedBy = record.CommandId;
        listing.Version++;
        record.State = MarketPurchaseState.Succeeded;
        record.ResultCode = "market-purchase-complete-placement-available";
        return record;
    }

    private MarketPurchaseRecord Commit(MarketPurchaseRecord record, MarketListing listing, string assetId)
    {
        if (!record.OwnershipCommitted)
        {
            var ownership = state.Ownership.Single(x => x.AssetId == assetId); ownership.Owner = Clone(record.Buyer); ownership.Version++;
            var fleet = state.Fleet.SingleOrDefault(x => x.AssetId == assetId); if (fleet != null) { fleet.OperationalState = FleetOperationalState.Stored; fleet.LastKnownLocation = listing.LocationId; fleet.Version++; }
            record.AssetId = assetId; record.OwnershipCommitted = true;
        }
        listing.State = MarketListingState.Sold; listing.ReservedBy = record.CommandId; listing.Version++;
        record.State = MarketPurchaseState.Succeeded; record.ResultCode = "market-purchase-complete"; return record;
    }

    private MarketPurchaseRecord Compensate(MarketPurchaseRecord record, MarketListing listing, Wallet wallet, string detail)
    {
        if (record.Debited)
        {
            wallet.Balance += record.Price; wallet.Version++; record.Debited = false;
            if (!state.Economy.Ledger.Any(x => x.EntryId == record.CommandId + ":market-compensation"))
                state.Economy.Ledger.Add(new LedgerEntry { EntryId = record.CommandId + ":market-compensation", CommandId = record.CommandId, Kind = LedgerEntryKind.VehiclePurchase, Credit = wallet.Account, Amount = record.Price, Detail = "finite-market-compensation;listing=" + listing.ListingId + ";reason=" + detail });
        }
        listing.State = MarketListingState.Available; listing.ReservedBy = null; listing.Version++;
        record.State = MarketPurchaseState.Compensated; record.ResultCode = "delivery-compensated:" + detail; return record;
    }

    private MarketPurchaseRecord CheckpointResult(MarketPurchaseRecord record, string phase)
    {
        if (TryCheckpoint(record, phase)) return record;
        if (record.State == MarketPurchaseState.Compensated) return record;
        record.State = MarketPurchaseState.ReconcileRequired;
        record.ResultCode = "market-purchase-reconcile:checkpoint-result-failed";
        return record;
    }

    private bool TryCheckpoint(MarketPurchaseRecord record, string phase)
    {
        try { return checkpoint.TryCheckpoint(record, phase); }
        catch { return false; }
    }

    private string? Validate(MarketPurchaseCommand command, MarketListing? listing)
    {
        if (listing == null || listing.State != MarketListingState.Available || listing.ExpiresTick <= state.Market.ClockTick) return "listing-unavailable";
        if (listing.Version != command.ExpectedListingVersion) return "listing-version-mismatch";
        var player = state.Economy.Players.SingleOrDefault(x => x.PlayerId == command.RequesterId); if (player == null || player.Version != command.ExpectedPlayerVersion) return "player-version-mismatch";
        if (command.Buyer.Key != (command.Payer.Kind == AccountKind.Player ? "Player:" + command.Payer.OwnerId : "Company:" + command.Payer.OwnerId)) return "buyer-payer-mismatch";
        if (command.Payer.Kind == AccountKind.Player && command.Payer.OwnerId != player.PlayerId) return "personal-payer-owner-required";
        if (command.Payer.Kind == AccountKind.Company)
        {
            var company = state.Economy.Companies.SingleOrDefault(x => x.CompanyId == command.Payer.OwnerId); if (company == null || player.CompanyId != company.CompanyId || command.ExpectedCompanyVersion != company.Version) return "company-version-or-membership-mismatch";
            if (company.LeaderId != player.PlayerId && (!company.DelegatedPermissions.TryGetValue(player.PlayerId, out var rights) || !rights.Contains(CompanyPermission.ManageFunds))) return "manage-funds-required";
        }
        var wallet = state.Economy.Wallets.SingleOrDefault(x => x.Account.Key == command.Payer.Key); if (wallet == null || wallet.Version != command.ExpectedWalletVersion) return "wallet-version-mismatch";
        return wallet.Balance < listing.Price ? "insufficient-funds" : null;
    }

    private MarketListing Listing(string id, MarketListingKind kind, string? assetId, MarketCatalogEntry catalog, string location, decimal condition, decimal factor)
    {
        var reference = ConditionReference(catalog, condition);
        var price = checked(decimal.ToInt64(decimal.Floor(reference * factor)) + catalog.TransferFee);
        return new MarketListing { ListingId = id, Kind = kind, AssetId = assetId, DefinitionId = catalog.DefinitionId, CategoryId = catalog.CategoryId, LocationId = location, Price = price, ReferenceValue = reference, Condition = condition, MarketFactor = factor, TransferFee = catalog.TransferFee, CreatedTick = state.Market.ClockTick, ExpiresTick = checked(state.Market.ClockTick + catalog.QuoteDurationTicks), State = MarketListingState.Available, Version = 1 };
    }

    private static long ConditionReference(MarketCatalogEntry catalog, decimal condition)
    {
        if (condition < 0m || condition > 1m) throw new ArgumentOutOfRangeException(nameof(condition));
        var conditionFactor = catalog.MinimumConditionFactor + ((1m - catalog.MinimumConditionFactor) * condition);
        return checked(decimal.ToInt64(decimal.Floor(catalog.BasePrice * conditionFactor)));
    }

    private MarketCatalogEntry Catalog(string definitionId)
    {
        var result = state.Market.Catalog.SingleOrDefault(x => x.DefinitionId == definitionId);
        if (result == null) throw new InvalidOperationException("Unknown vehicle definition is not priced: " + definitionId);
        return result;
    }

    private decimal NextFactor(decimal minimum, decimal maximum)
    {
        unchecked { state.Market.RandomState = state.Market.RandomState * 6364136223846793005UL + 1442695040888963407UL; }
        var unit = (decimal)(state.Market.RandomState >> 11) / 9007199254740991m;
        return Math.Round(minimum + ((maximum - minimum) * unit), 6, MidpointRounding.AwayFromZero);
    }

    private void RequireHost() { if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out var reason)) throw new InvalidOperationException(reason); }
    private static void RequireIdentity(string listingId, string locationId) { if (string.IsNullOrWhiteSpace(listingId) || string.IsNullOrWhiteSpace(locationId)) throw new ArgumentException("Listing and location IDs are required."); }
    private static void Require(MarketPurchaseCommand command) { if (command == null || string.IsNullOrWhiteSpace(command.CommandId) || string.IsNullOrWhiteSpace(command.RequesterId) || string.IsNullOrWhiteSpace(command.ListingId) || command.Buyer == null || command.Payer == null) throw new ArgumentException("Complete market purchase command is required."); }
    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));
    private static bool ValidGuid(string? value) => Guid.TryParse(value, out var guid) && guid != Guid.Empty;
    private static AssetOwnerRef Clone(AssetOwnerRef value) => new AssetOwnerRef { Kind = value.Kind, OwnerId = value.OwnerId };
    private static string Fingerprint(MarketPurchaseCommand command) => string.Join("|", command.RequesterId, command.ListingId, command.Buyer.Key, command.Payer.Key, command.ExpectedListingVersion, command.ExpectedWalletVersion, command.ExpectedPlayerVersion, command.ExpectedCompanyVersion);
    private static MarketPurchaseRecord NewRecord(MarketPurchaseCommand command, MarketListing? listing, string fingerprint) => new MarketPurchaseRecord { CommandId = command.CommandId, Fingerprint = fingerprint, RequesterId = command.RequesterId, ListingId = command.ListingId, AssetId = listing?.AssetId, Buyer = Clone(command.Buyer), Payer = new AccountRef { Kind = command.Payer.Kind, OwnerId = command.Payer.OwnerId }, Price = listing?.Price ?? 0 };
    private static MarketPurchaseRecord Reject(MarketPurchaseRecord record, string code) { record.State = MarketPurchaseState.Rejected; record.ResultCode = code; return record; }
    private static MarketPurchaseRecord Pending(MarketPurchaseRecord record, string code) { record.State = MarketPurchaseState.ReconcileRequired; record.ResultCode = code; return record; }
}

public static class FiniteMarketValidation
{
    public static void Validate(FiniteMarketState market)
    {
        if (market == null || market.Schema != FiniteMarketState.CurrentSchema || market.SchemaVersion != FiniteMarketState.CurrentVersion || market.ClockTick < 0) throw new InvalidOperationException("Invalid finite market state.");
        if (market.Catalog.GroupBy(x => x.DefinitionId).Any(x => x.Count() != 1) || market.Stock.GroupBy(x => x.Key).Any(x => x.Count() != 1) || market.Listings.GroupBy(x => x.ListingId).Any(x => x.Count() != 1) || market.Purchases.GroupBy(x => x.CommandId).Any(x => x.Count() != 1)) throw new InvalidOperationException("Duplicate finite market identity.");
        foreach (var c in market.Catalog) if (string.IsNullOrWhiteSpace(c.DefinitionId) || string.IsNullOrWhiteSpace(c.CategoryId) || c.BasePrice <= 0 || c.MinimumMarketFactor <= 0m || c.MaximumMarketFactor < c.MinimumMarketFactor || c.MinimumConditionFactor <= 0m || c.MinimumConditionFactor > 1m || c.TransferFee < 0 || c.BuybackRate < 0m || c.BuybackRate >= 1m || c.QuoteDurationTicks <= 0) throw new InvalidOperationException("Invalid market catalog entry.");
        foreach (var s in market.Stock) if (string.IsNullOrWhiteSpace(s.LocationId) || string.IsNullOrWhiteSpace(s.DefinitionId) || s.Available < 0 || s.Capacity < 0 || s.Available > s.Capacity || !market.Catalog.Any(c => c.DefinitionId == s.DefinitionId)) throw new InvalidOperationException("Invalid finite stock entry.");
        foreach (var l in market.Listings) if (string.IsNullOrWhiteSpace(l.ListingId) || string.IsNullOrWhiteSpace(l.DefinitionId) || string.IsNullOrWhiteSpace(l.LocationId) || l.Price < 0 || l.ReferenceValue < 0 || l.Condition < 0m || l.Condition > 1m || l.MarketFactor <= 0m || l.TransferFee < 0 || l.ExpiresTick <= l.CreatedTick || !market.Catalog.Any(c => c.DefinitionId == l.DefinitionId)) throw new InvalidOperationException("Invalid market listing.");
    }
}
