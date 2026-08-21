namespace ShippingCalculator.Api.Models;

/// <summary>Response body for a successful <c>POST /api/shipping/calculate</c>.</summary>
public sealed record ShippingCalculateResponse(decimal TotalCost, ShippingBreakdownResponse Breakdown);
