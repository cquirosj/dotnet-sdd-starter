using ShippingCalculator.Domain.Models;

namespace ShippingCalculator.Api.Models;

/// <summary>Request body for <c>POST /api/shipping/calculate</c>.</summary>
public sealed record ShippingCalculateRequest(decimal WeightKg, DistanceZone Zone, decimal OrderTotal);
