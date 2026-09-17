using System;
using System.Linq;
namespace BDVM.Domain;
public static class GameplayCatalogPrices
{
    public static long BasePrice(string definitionId, FleetVehicleKind kind)
    {
        var id = new string(definitionId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (kind == FleetVehicleKind.Locomotive)
        {
            if (id.Contains("DE2")) return 40000;
            if (id.Contains("DH4")) return 90000;
            if (id.Contains("DM3")) return 65000;
            if (id.Contains("DE6")) return 180000;
            if (id.Contains("S060")) return 55000;
            if (id.Contains("S282")) return 120000;
            if (id.Contains("BE2")) return 50000;
            if (id.Contains("MICRO")) return 20000;
            return 100000; // Explicit fallback for unconfigured custom locomotives.
        }
        if (kind == FleetVehicleKind.PassengerCar) return 25000;
        if (id.Contains("REFRIG")) return 22000;
        if (id.Contains("TANK")) return 20000;
        if (id.Contains("HOPPER")) return 18000;
        if (id.Contains("BOX")) return 15000;
        if (id.Contains("CABOOSE")) return 8000;
        return 12000;
    }

    public static void UpgradeLegacyDefault(FiniteMarketState market, MarketCatalogEntry entry, FleetVehicleKind kind)
    {
        var old = kind == FleetVehicleKind.Locomotive ? (entry.DefinitionId == "LocoDE2" ? 40000 : 150000) : kind == FleetVehicleKind.PassengerCar ? 20000 : 10000;
        if (entry.BasePrice != old || entry.TransferFee != 0 || entry.BuybackRate != 0.5m || entry.MinimumMarketFactor != 0.8m || entry.MaximumMarketFactor != 1.2m || entry.QuoteDurationTicks != 100) return;
        entry.BasePrice = BasePrice(entry.DefinitionId, kind); entry.MinimumMarketFactor = 0.9m; entry.MaximumMarketFactor = 1.1m; entry.Version++;
        foreach (var listing in market.Listings.Where(value => value.DefinitionId == entry.DefinitionId && value.Kind == MarketListingKind.NewOrder && value.State == MarketListingState.Available))
        {
            listing.MarketFactor = Math.Max(entry.MinimumMarketFactor, Math.Min(entry.MaximumMarketFactor, listing.MarketFactor));
            listing.ReferenceValue = decimal.ToInt64(decimal.Floor(entry.BasePrice * (entry.MinimumConditionFactor + (1m - entry.MinimumConditionFactor) * listing.Condition)));
            listing.Price = checked(decimal.ToInt64(decimal.Floor(listing.ReferenceValue * listing.MarketFactor)) + listing.TransferFee); listing.Version++;
        }
    }
}
