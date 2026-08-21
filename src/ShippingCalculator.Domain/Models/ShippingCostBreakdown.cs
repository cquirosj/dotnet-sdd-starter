namespace ShippingCalculator.Domain.Models;

/// <summary>
/// The intermediate steps behind a <see cref="ShippingCostResult"/>, so every
/// stage of the calculation is visible and testable rather than just the
/// final total.
/// </summary>
public sealed record ShippingCostBreakdown(
    decimal BaseRate,
    decimal ZoneMultiplier,
    decimal ZonedRate,
    bool FreeShippingApplied);
