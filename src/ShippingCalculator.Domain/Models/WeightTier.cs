namespace ShippingCalculator.Domain.Models;

/// <summary>
/// The weight bracket a parcel falls into. Determines the base shipping rate
/// before any zone multiplier or free-shipping rule is applied (see
/// <c>ShippingCostService</c> for the bracket boundaries and rates).
/// </summary>
public enum WeightTier
{
    UpTo1Kg,
    UpTo5Kg,
    UpTo10Kg,
    UpTo20Kg,
    Over20Kg,
}
