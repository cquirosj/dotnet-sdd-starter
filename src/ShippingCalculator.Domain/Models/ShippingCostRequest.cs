namespace ShippingCalculator.Domain.Models;

/// <summary>
/// Input to <c>ShippingCostService.Calculate</c>: the parcel weight, the
/// destination zone, and the order's total value (used by the free-shipping
/// rule).
/// </summary>
public sealed record ShippingCostRequest(decimal WeightKg, DistanceZone Zone, decimal OrderTotal);
