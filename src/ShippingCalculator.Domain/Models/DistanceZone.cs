namespace ShippingCalculator.Domain.Models;

/// <summary>
/// The destination zone for a shipment. Determines the zone multiplier applied
/// to the weight-tier base rate (see <c>ShippingCostService</c>).
/// </summary>
public enum DistanceZone
{
    Domestic,
    European,
    International,
}
