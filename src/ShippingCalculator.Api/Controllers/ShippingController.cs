using Microsoft.AspNetCore.Mvc;
using ShippingCalculator.Api.Models;
using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Services;

namespace ShippingCalculator.Api.Controllers;

/// <summary>
/// REST endpoints for shipping cost calculation. Thin by design: receives the
/// request, delegates to <see cref="IShippingCostService"/>, and maps the
/// resulting <c>Result</c> to an HTTP response. No business logic here.
/// </summary>
[ApiController]
[Route("api/shipping")]
public sealed class ShippingController : ControllerBase
{
    private readonly IShippingCostService _shippingCostService;

    public ShippingController(IShippingCostService shippingCostService)
    {
        _shippingCostService = shippingCostService;
    }

    /// <summary>Calculates the shipping cost for a parcel.</summary>
    /// <response code="200">The calculated cost and its breakdown.</response>
    /// <response code="400">The request failed validation (e.g. non-positive weight).</response>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ShippingCalculateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult Calculate([FromBody] ShippingCalculateRequest request)
    {
        var domainRequest = new ShippingCostRequest(request.WeightKg, request.Zone, request.OrderTotal);

        return _shippingCostService.Calculate(domainRequest).Match<IActionResult>(
            onSuccess: result => Ok(new ShippingCalculateResponse(
                result.TotalCost,
                new ShippingBreakdownResponse(
                    result.Breakdown.BaseRate,
                    result.Breakdown.ZoneMultiplier,
                    result.Breakdown.ZonedRate,
                    result.Breakdown.FreeShippingApplied))),
            onFailure: error => BadRequest(new ErrorResponse(error.Code, error.Message)));
    }
}
