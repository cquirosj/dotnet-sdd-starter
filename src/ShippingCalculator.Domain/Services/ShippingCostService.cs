using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Shared;

namespace ShippingCalculator.Domain.Services;

/// <summary>
/// Pure calculation service for shipping costs. No HTTP, no persistence —
/// testable with plain xUnit and no framework context.
///
/// Processing order (must not be reordered — code and tests both follow it):
///   1. Base rate from weight tier.
///   2. Zone multiplier (domestic x1.0, European x1.5, international x2.5).
///   3. Free-shipping check (domestic order total &gt;= 75.00 -&gt; 0.00).
/// </summary>
public sealed class ShippingCostService : IShippingCostService
{
    private const decimal FreeShippingOrderTotalThreshold = 75.00m;

    private static readonly IReadOnlyDictionary<WeightTier, decimal> BaseRates = new Dictionary<WeightTier, decimal>
    {
        [WeightTier.UpTo1Kg] = 5.00m,
        [WeightTier.UpTo5Kg] = 10.00m,
        [WeightTier.UpTo10Kg] = 18.00m,
        [WeightTier.UpTo20Kg] = 30.00m,
        [WeightTier.Over20Kg] = 45.00m,
    };

    private static readonly IReadOnlyDictionary<DistanceZone, decimal> ZoneMultipliers = new Dictionary<DistanceZone, decimal>
    {
        [DistanceZone.Domestic] = 1.0m,
        [DistanceZone.European] = 1.5m,
        [DistanceZone.International] = 2.5m,
    };

    public Result<ShippingCostResult, Error> Calculate(ShippingCostRequest request)
    {
        if (request.WeightKg <= 0m)
        {
            return Result<ShippingCostResult, Error>.Failure(
                new Error("INVALID_WEIGHT", "Weight must be greater than zero kilograms."));
        }

        if (request.OrderTotal < 0m)
        {
            return Result<ShippingCostResult, Error>.Failure(
                new Error("INVALID_ORDER_TOTAL", "Order total must not be negative."));
        }

        var weightTier = DetermineWeightTier(request.WeightKg);
        var baseRate = BaseRates[weightTier];
        var zoneMultiplier = ZoneMultipliers[request.Zone];
        var zonedRate = Round(baseRate * zoneMultiplier);

        var freeShippingApplied =
            request.Zone == DistanceZone.Domestic && request.OrderTotal >= FreeShippingOrderTotalThreshold;

        var totalCost = freeShippingApplied ? 0.00m : zonedRate;

        var breakdown = new ShippingCostBreakdown(baseRate, zoneMultiplier, zonedRate, freeShippingApplied);
        return Result<ShippingCostResult, Error>.Success(new ShippingCostResult(totalCost, breakdown));
    }

    private static WeightTier DetermineWeightTier(decimal weightKg) => weightKg switch
    {
        <= 1.0m => WeightTier.UpTo1Kg,
        <= 5.0m => WeightTier.UpTo5Kg,
        <= 10.0m => WeightTier.UpTo10Kg,
        <= 20.0m => WeightTier.UpTo20Kg,
        _ => WeightTier.Over20Kg,
    };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
