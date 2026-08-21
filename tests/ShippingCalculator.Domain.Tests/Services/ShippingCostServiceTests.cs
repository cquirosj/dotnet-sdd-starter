using Shouldly;
using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Services;

namespace ShippingCalculator.Domain.Tests.Services;

/// <summary>
/// Service/unit tests for <see cref="ShippingCostService"/>. Plain xUnit, no
/// framework context — these prove the business logic and its
/// <c>Result&lt;TValue, Error&gt;</c> outcomes directly.
/// </summary>
public class ShippingCostServiceTests
{
    protected readonly ShippingCostService _service = new();

    public class WeightTierBaseRates : ShippingCostServiceTests
    {
        [Theory(DisplayName = "The base rate is looked up from the parcel's weight tier")]
        [InlineData(1.0, "5.00")]
        [InlineData(5.0, "10.00")]
        [InlineData(10.0, "18.00")]
        [InlineData(20.0, "30.00")]
        [InlineData(20.01, "45.00")]
        public void BaseRateIsLookedUpFromWeightTier(double weightKg, string expectedBaseRate)
        {
            var request = new ShippingCostRequest((decimal)weightKg, DistanceZone.Domestic, 0.00m);

            var result = _service.Calculate(request);

            result.IsSuccess.ShouldBeTrue();
            result.Value.Breakdown.BaseRate.ShouldBe(decimal.Parse(expectedBaseRate));
        }

        [Fact(DisplayName = "The one where a 3.2kg parcel falls in the 1-5kg tier at £10.00")]
        public void ParcelJustOverOneKgFallsInNextTier()
        {
            var request = new ShippingCostRequest(3.2m, DistanceZone.Domestic, 0.00m);

            var result = _service.Calculate(request);

            result.Value.Breakdown.BaseRate.ShouldBe(10.00m);
        }

        [Fact(DisplayName = "The one where a parcel just over 1kg no longer qualifies for the up-to-1kg rate")]
        public void ParcelAtOneKgIsStillInTheFirstTier()
        {
            var atBoundary = _service.Calculate(new ShippingCostRequest(1.0m, DistanceZone.Domestic, 0.00m));
            var justOver = _service.Calculate(new ShippingCostRequest(1.01m, DistanceZone.Domestic, 0.00m));

            atBoundary.Value.Breakdown.BaseRate.ShouldBe(5.00m);
            justOver.Value.Breakdown.BaseRate.ShouldBe(10.00m);
        }
    }

    public class ZoneMultiplier : ShippingCostServiceTests
    {
        [Theory(DisplayName = "The zoned rate applies the zone multiplier to the base rate")]
        [InlineData(DistanceZone.Domestic, "1.0", "5.00")]
        [InlineData(DistanceZone.European, "1.5", "7.50")]
        [InlineData(DistanceZone.International, "2.5", "12.50")]
        public void ZonedRateAppliesMultiplierToBaseRate(DistanceZone zone, string expectedMultiplier, string expectedZonedRate)
        {
            var request = new ShippingCostRequest(1.0m, zone, 0.00m);

            var result = _service.Calculate(request);

            result.Value.Breakdown.ZoneMultiplier.ShouldBe(decimal.Parse(expectedMultiplier));
            result.Value.Breakdown.ZonedRate.ShouldBe(decimal.Parse(expectedZonedRate));
        }
    }

    public class FreeShipping : ShippingCostServiceTests
    {
        [Fact(DisplayName = "The one where a domestic order of exactly £75.00 gets free shipping")]
        public void DomesticOrderAtExactlySeventyFiveGetsFreeShipping()
        {
            var request = new ShippingCostRequest(3.0m, DistanceZone.Domestic, 75.00m);

            var result = _service.Calculate(request);

            result.Value.Breakdown.FreeShippingApplied.ShouldBeTrue();
            result.Value.TotalCost.ShouldBe(0.00m);
        }

        [Fact(DisplayName = "The one where a domestic order of £74.99 does not get free shipping")]
        public void DomesticOrderJustUnderSeventyFiveDoesNotGetFreeShipping()
        {
            var request = new ShippingCostRequest(3.0m, DistanceZone.Domestic, 74.99m);

            var result = _service.Calculate(request);

            result.Value.Breakdown.FreeShippingApplied.ShouldBeFalse();
            result.Value.TotalCost.ShouldBe(result.Value.Breakdown.ZonedRate);
        }

        [Fact(DisplayName = "The one where a European order over £75.00 still pays the zoned rate")]
        public void NonDomesticOrderNeverGetsFreeShippingRegardlessOfOrderTotal()
        {
            var request = new ShippingCostRequest(3.0m, DistanceZone.European, 500.00m);

            var result = _service.Calculate(request);

            result.Value.Breakdown.FreeShippingApplied.ShouldBeFalse();
            result.Value.TotalCost.ShouldBe(result.Value.Breakdown.ZonedRate);
        }
    }

    public class Validation : ShippingCostServiceTests
    {
        [Fact(DisplayName = "The one where zero weight is rejected")]
        public void ZeroWeightIsRejected()
        {
            var request = new ShippingCostRequest(0.0m, DistanceZone.Domestic, 10.00m);

            var result = _service.Calculate(request);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("INVALID_WEIGHT");
        }

        [Fact(DisplayName = "The one where negative weight is rejected")]
        public void NegativeWeightIsRejected()
        {
            var request = new ShippingCostRequest(-1.0m, DistanceZone.Domestic, 10.00m);

            var result = _service.Calculate(request);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("INVALID_WEIGHT");
        }

        [Fact(DisplayName = "The one where negative order total is rejected")]
        public void NegativeOrderTotalIsRejected()
        {
            var request = new ShippingCostRequest(1.0m, DistanceZone.Domestic, -0.01m);

            var result = _service.Calculate(request);

            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("INVALID_ORDER_TOTAL");
        }

        [Fact(DisplayName = "Reading Value on a failed result throws — that is a programmer bug, not a domain failure")]
        public void ReadingValueOnFailureThrows()
        {
            var result = _service.Calculate(new ShippingCostRequest(-1.0m, DistanceZone.Domestic, 0.00m));

            Should.Throw<InvalidOperationException>(() => result.Value);
        }
    }
}
