using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

[DataContract]
public sealed class DynamicMarketPolicy
{
    [DataMember(Name = "categoryId", Order = 1)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "minimumFactor", Order = 2)] public decimal MinimumFactor { get; set; } = 0.8m;
    [DataMember(Name = "maximumFactor", Order = 3)] public decimal MaximumFactor { get; set; } = 1.2m;
    [DataMember(Name = "smoothing", Order = 4)] public decimal Smoothing { get; set; } = 0.2m;
    [DataMember(Name = "maximumStep", Order = 5)] public decimal MaximumStep { get; set; } = 0.05m;
    [DataMember(Name = "supplyWeight", Order = 6)] public decimal SupplyWeight { get; set; } = 0.25m;
    [DataMember(Name = "demandWeight", Order = 7)] public decimal DemandWeight { get; set; } = 0.35m;
    [DataMember(Name = "utilizationWeight", Order = 8)] public decimal UtilizationWeight { get; set; } = 0.2m;
    [DataMember(Name = "version", Order = 9)] public long Version { get; set; } = 1;
}

[DataContract]
public sealed class DynamicMarketMetric
{
    [DataMember(Name = "categoryId", Order = 1)] public string CategoryId { get; set; } = "";
    [DataMember(Name = "supplyRatio", Order = 2)] public decimal SupplyRatio { get; set; }
    [DataMember(Name = "demandRatio", Order = 3)] public decimal DemandRatio { get; set; }
    [DataMember(Name = "utilizationRatio", Order = 4)] public decimal UtilizationRatio { get; set; }
    [DataMember(Name = "lessorAvailabilityRatio", Order = 5)] public decimal LessorAvailabilityRatio { get; set; }
    [DataMember(Name = "rawFactor", Order = 6)] public decimal RawFactor { get; set; } = 1m;
    [DataMember(Name = "smoothedFactor", Order = 7)] public decimal SmoothedFactor { get; set; } = 1m;
    [DataMember(Name = "calculatedTick", Order = 8)] public long CalculatedTick { get; set; }
    [DataMember(Name = "version", Order = 9)] public long Version { get; set; }
}

[DataContract]
public sealed class AssetProfitability
{
    [DataMember(Name = "assetId", Order = 1)] public string AssetId { get; set; } = "";
    [DataMember(Name = "operatingRevenue", Order = 2)] public long OperatingRevenue { get; set; }
    [DataMember(Name = "operatingCosts", Order = 3)] public long OperatingCosts { get; set; }
    [DataMember(Name = "netOperatingResult", Order = 4)] public long NetOperatingResult { get; set; }
    [DataMember(Name = "acquisitionCash", Order = 5)] public long AcquisitionCash { get; set; }
    [DataMember(Name = "completedServices", Order = 6)] public int CompletedServices { get; set; }
    [DataMember(Name = "version", Order = 7)] public long Version { get; set; }
}

[DataContract]
public sealed class DynamicEconomyState
{
    [DataMember(Name = "policies", Order = 1)] public List<DynamicMarketPolicy> Policies { get; set; } = new List<DynamicMarketPolicy>();
    [DataMember(Name = "metrics", Order = 2)] public List<DynamicMarketMetric> Metrics { get; set; } = new List<DynamicMarketMetric>();
    [DataMember(Name = "profitability", Order = 3)] public List<AssetProfitability> Profitability { get; set; } = new List<AssetProfitability>();
    [DataMember(Name = "commands", Order = 4)] public List<MissionAssignmentCommand> Commands { get; set; } = new List<MissionAssignmentCommand>();
    [DataMember(Name = "lastCalculatedTick", Order = 5)] public long LastCalculatedTick { get; set; }
}

public sealed class DynamicEconomyEngine
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    public DynamicEconomyEngine(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority) { this.state = state ?? throw new ArgumentNullException(nameof(state)); this.authority = authority ?? throw new ArgumentNullException(nameof(authority)); VehicleAcquisitionPersistence.Validate(state); }

    public DynamicMarketPolicy ConfigurePolicy(string commandId, string categoryId, decimal minimum, decimal maximum, decimal smoothing,
        decimal maximumStep, decimal supplyWeight, decimal demandWeight, decimal utilizationWeight)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = string.Join("|", "policy", categoryId, minimum, maximum, smoothing, maximumStep, supplyWeight, demandWeight, utilizationWeight); var replay = Command(commandId, fingerprint); if (replay != null) return state.DynamicEconomy.Policies.Single(x => x.CategoryId == replay.AssignmentId);
            if (string.IsNullOrWhiteSpace(categoryId) || minimum <= 0m || maximum < minimum || smoothing <= 0m || smoothing > 1m || maximumStep <= 0m || supplyWeight < 0m || demandWeight < 0m || utilizationWeight < 0m) throw new ArgumentException("Invalid dynamic market policy.");
            var policy = state.DynamicEconomy.Policies.SingleOrDefault(x => x.CategoryId == categoryId);
            if (policy == null) { policy = new DynamicMarketPolicy { CategoryId = categoryId }; state.DynamicEconomy.Policies.Add(policy); }
            else policy.Version++;
            policy.MinimumFactor = minimum; policy.MaximumFactor = maximum; policy.Smoothing = smoothing; policy.MaximumStep = maximumStep; policy.SupplyWeight = supplyWeight; policy.DemandWeight = demandWeight; policy.UtilizationWeight = utilizationWeight;
            Record(commandId, fingerprint, categoryId, "dynamic-policy-configured"); return policy;
        }
    }

    public IReadOnlyList<DynamicMarketMetric> Recalculate(string commandId, long tick)
    {
        lock (gate)
        {
            RequireHost(); var fingerprint = "recalculate|" + tick; var replay = Command(commandId, fingerprint); if (replay != null) return state.DynamicEconomy.Metrics.ToArray();
            if (state.DynamicEconomy.Commands.Any(x => x.ResultCode == "dynamic-economy-recalculated") && tick <= state.DynamicEconomy.LastCalculatedTick) throw new InvalidOperationException("Dynamic economy clock must advance and cannot reroll an existing state.");
            foreach (var policy in state.DynamicEconomy.Policies.OrderBy(x => x.CategoryId, StringComparer.Ordinal)) Calculate(policy, tick);
            state.DynamicEconomy.LastCalculatedTick = tick; RebuildProfitability(); Record(commandId, fingerprint, "all", "dynamic-economy-recalculated"); return state.DynamicEconomy.Metrics.ToArray();
        }
    }

    public decimal FactorForDefinition(string definitionId)
    {
        lock (gate)
        {
            var catalog = state.Market.Catalog.Single(x => x.DefinitionId == definitionId); var metric = state.DynamicEconomy.Metrics.SingleOrDefault(x => x.CategoryId == catalog.CategoryId);
            return metric == null ? 1m : Math.Max(catalog.MinimumMarketFactor, Math.Min(catalog.MaximumMarketFactor, metric.SmoothedFactor));
        }
    }

    private void Calculate(DynamicMarketPolicy policy, long tick)
    {
        var definitions = state.Market.Catalog.Where(x => x.CategoryId == policy.CategoryId).Select(x => x.DefinitionId).ToHashSet(StringComparer.Ordinal);
        var stock = state.Market.Stock.Where(x => definitions.Contains(x.DefinitionId)).ToArray(); var stockCapacity = stock.Sum(x => x.Capacity); var supply = stockCapacity == 0 ? 0.5m : Ratio(stock.Sum(x => x.Available), stockCapacity);
        var categoryAssets = state.Assets.Assets.Where(x => definitions.Contains(x.DefinitionId)).Select(x => x.AssetId).ToHashSet(StringComparer.Ordinal);
        var managed = state.Fleet.Where(x => categoryAssets.Contains(x.AssetId)).ToArray(); var used = managed.Count(x => x.OperationalState == FleetOperationalState.Reserved || x.OperationalState == FleetOperationalState.InService || x.OperationalState == FleetOperationalState.Maintenance);
        var utilization = managed.Length == 0 ? 0.5m : Ratio(used, managed.Length);
        var lessorContracts = state.OutboundLeases.Where(x => x.AssetIds.Any(categoryAssets.Contains) && x.State != OutboundLeaseState.Returned && x.State != OutboundLeaseState.Cancelled).ToArray(); var lessorAvailable = lessorContracts.Length == 0 ? 0.5m : Ratio(lessorContracts.Count(x => x.State == OutboundLeaseState.Offered), lessorContracts.Length);
        var sold = state.Market.Listings.Count(x => x.CategoryId == policy.CategoryId && x.State == MarketListingState.Sold); var totalListings = state.Market.Listings.Count(x => x.CategoryId == policy.CategoryId); var marketDemand = totalListings == 0 ? 0.5m : Ratio(sold, totalListings);
        var passengerDemand = state.PassengerRoutes.Count == 0 ? 0.5m : state.PassengerRoutes.Average(x => Ratio(x.DemandUnits, x.MaximumDemandUnits));
        var industrialDemand = state.IndustrialStocks.Count == 0 ? 0.5m : state.IndustrialStocks.Average(x => x.Capacity == 0 ? 0m : 1m - Ratio(x.OnHand - x.ReservedOutbound, x.Capacity));
        var category = policy.CategoryId.ToLowerInvariant(); var demand = category.Contains("passenger") || category.Contains("coach") ? passengerDemand : category.Contains("freight") || category.Contains("wagon") ? industrialDemand : marketDemand;
        var effectiveSupply = Clamp((supply + lessorAvailable) / 2m, 0m, 1m); var raw = 1m + policy.DemandWeight * (demand - 0.5m) + policy.UtilizationWeight * (utilization - 0.5m) - policy.SupplyWeight * (effectiveSupply - 0.5m); raw = Clamp(raw, policy.MinimumFactor, policy.MaximumFactor);
        var metric = state.DynamicEconomy.Metrics.SingleOrDefault(x => x.CategoryId == policy.CategoryId); if (metric == null) { metric = new DynamicMarketMetric { CategoryId = policy.CategoryId, SmoothedFactor = 1m }; state.DynamicEconomy.Metrics.Add(metric); }
        var wanted = metric.SmoothedFactor + (raw - metric.SmoothedFactor) * policy.Smoothing; var step = Clamp(wanted - metric.SmoothedFactor, -policy.MaximumStep, policy.MaximumStep);
        metric.SupplyRatio = supply; metric.DemandRatio = demand; metric.UtilizationRatio = utilization; metric.LessorAvailabilityRatio = lessorAvailable; metric.RawFactor = raw; metric.SmoothedFactor = Clamp(metric.SmoothedFactor + step, policy.MinimumFactor, policy.MaximumFactor); metric.CalculatedTick = tick; metric.Version++;
    }

    private void RebuildProfitability()
    {
        foreach (var asset in state.Fleet)
        {
            long revenue = 0; long costs = 0; var services = 0;
            foreach (var assignment in state.Assignments.Where(x => x.State == MissionAssignmentState.Completed && x.AssetIds.Contains(asset.AssetId))) { revenue = checked(revenue + assignment.ActualRevenue / assignment.AssetIds.Count); services++; }
            foreach (var lease in state.OutboundLeases.Where(x => x.AssetIds.Contains(asset.AssetId))) revenue = checked(revenue + lease.Installments.Where(x => x.Credited).Sum(x => x.Amount) / lease.AssetIds.Count);
            costs = checked(costs + state.OperatingCosts.Where(x => x.AssetId == asset.AssetId && x.State == OperatingCostState.Settled).Sum(x => x.ActualCost));
            foreach (var lease in state.Leases.Where(x => x.AssetIds.Contains(asset.AssetId))) costs = checked(costs + lease.Installments.Where(x => x.Paid).Sum(x => x.Amount) / lease.AssetIds.Count);
            var acquisition = state.Acquisitions.LastOrDefault(x => x.AssetId == asset.AssetId && x.State == AcquisitionState.Succeeded)?.Price ?? 0;
            var record = state.DynamicEconomy.Profitability.SingleOrDefault(x => x.AssetId == asset.AssetId); if (record == null) { record = new AssetProfitability { AssetId = asset.AssetId }; state.DynamicEconomy.Profitability.Add(record); }
            record.OperatingRevenue = revenue; record.OperatingCosts = costs; record.NetOperatingResult = checked(revenue - costs); record.AcquisitionCash = acquisition; record.CompletedServices = services; record.Version++;
        }
    }

    private MissionAssignmentCommand? Command(string id, string fingerprint) { var r = state.DynamicEconomy.Commands.SingleOrDefault(x => x.CommandId == id); if (r != null && r.Fingerprint != fingerprint) throw new InvalidOperationException("Dynamic economy command ID payload conflict."); return r; }
    private void Record(string id, string fingerprint, string subject, string result) => state.DynamicEconomy.Commands.Add(new MissionAssignmentCommand { CommandId = id, Fingerprint = fingerprint, AssignmentId = subject, ResultCode = result });
    private void RequireHost() { if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out var reason)) throw new InvalidOperationException(reason); }
    private static decimal Ratio(long value, long maximum) => maximum <= 0 ? 0m : Clamp((decimal)value / maximum, 0m, 1m);
    private static decimal Ratio(decimal value, decimal maximum) => maximum <= 0m ? 0m : Clamp(value / maximum, 0m, 1m);
    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) => Math.Max(minimum, Math.Min(maximum, value));
}

public static class DynamicEconomyValidation
{
    public static void Validate(DynamicEconomyState state)
    {
        if (state == null || state.Policies.GroupBy(x => x.CategoryId).Any(x => x.Count() != 1) || state.Metrics.GroupBy(x => x.CategoryId).Any(x => x.Count() != 1) || state.Profitability.GroupBy(x => x.AssetId).Any(x => x.Count() != 1) || state.Commands.GroupBy(x => x.CommandId).Any(x => x.Count() != 1)) throw new InvalidOperationException("Invalid or duplicate dynamic economy state.");
        foreach (var p in state.Policies) if (string.IsNullOrWhiteSpace(p.CategoryId) || p.MinimumFactor <= 0m || p.MaximumFactor < p.MinimumFactor || p.Smoothing <= 0m || p.Smoothing > 1m || p.MaximumStep <= 0m || p.SupplyWeight < 0m || p.DemandWeight < 0m || p.UtilizationWeight < 0m) throw new InvalidOperationException("Invalid dynamic market policy.");
        foreach (var m in state.Metrics) if (string.IsNullOrWhiteSpace(m.CategoryId) || m.SupplyRatio < 0m || m.SupplyRatio > 1m || m.DemandRatio < 0m || m.DemandRatio > 1m || m.UtilizationRatio < 0m || m.UtilizationRatio > 1m || m.LessorAvailabilityRatio < 0m || m.LessorAvailabilityRatio > 1m || m.RawFactor <= 0m || m.SmoothedFactor <= 0m) throw new InvalidOperationException("Invalid dynamic market metric.");
    }
}
