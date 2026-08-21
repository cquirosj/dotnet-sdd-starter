namespace ShippingCalculator.Domain.Models;

/// <summary>The successful outcome of a shipping cost calculation.</summary>
public sealed record ShippingCostResult(decimal TotalCost, ShippingCostBreakdown Breakdown);
