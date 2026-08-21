using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Shared;

namespace ShippingCalculator.Domain.Services;

public interface IShippingCostService
{
    /// <summary>
    /// Calculates the shipping cost for a parcel, or a validation
    /// <see cref="Error"/> if the request is invalid. Never throws for
    /// expected validation failures.
    /// </summary>
    Result<ShippingCostResult, Error> Calculate(ShippingCostRequest request);
}
