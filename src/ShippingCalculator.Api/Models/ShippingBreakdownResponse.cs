namespace ShippingCalculator.Api.Models;

/// <summary>The intermediate steps of a shipping cost calculation.</summary>
public sealed record ShippingBreakdownResponse(
    decimal BaseRate,
    decimal ZoneMultiplier,
    decimal ZonedRate,
    bool FreeShippingApplied);
