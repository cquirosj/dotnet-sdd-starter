using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using ShippingCalculator.Api.Models;
using ShippingCalculator.Domain.Models;

namespace ShippingCalculator.Api.Tests;

/// <summary>
/// Acceptance tests for <c>POST /api/shipping/calculate</c>. Full HTTP
/// round-trip via <see cref="WebApplicationFactory{TEntryPoint}"/> — no mocks,
/// no direct calls into the service or domain layer.
/// </summary>
public class ShippingCalculateAcceptanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient _client;

    public ShippingCalculateAcceptanceTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public class WeightTiers : ShippingCalculateAcceptanceTests
    {
        public WeightTiers(WebApplicationFactory<Program> factory) : base(factory)
        {
        }

        [Theory(DisplayName = "The base rate is looked up from the parcel's weight tier")]
        [InlineData(1.0, 5.00)]
        [InlineData(5.0, 10.00)]
        [InlineData(10.0, 18.00)]
        [InlineData(20.0, 30.00)]
        [InlineData(20.01, 45.00)]
        public async Task BaseRateIsLookedUpFromWeightTier(decimal weightKg, decimal expectedBaseRate)
        {
            var request = new ShippingCalculateRequest(weightKg, DistanceZone.Domestic, 0.00m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
            body!.Breakdown.BaseRate.ShouldBe(expectedBaseRate);
        }
    }

    public class DistanceZones : ShippingCalculateAcceptanceTests
    {
        public DistanceZones(WebApplicationFactory<Program> factory) : base(factory)
        {
        }

        [Fact(DisplayName = "The one where a 3.2kg European parcel costs £15.00")]
        public async Task EuropeanParcelAppliesOnePointFiveMultiplier()
        {
            var request = new ShippingCalculateRequest(3.2m, DistanceZone.European, 50.00m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
            body!.Breakdown.BaseRate.ShouldBe(10.00m);
            body.Breakdown.ZoneMultiplier.ShouldBe(1.5m);
            body.Breakdown.ZonedRate.ShouldBe(15.00m);
            body.TotalCost.ShouldBe(15.00m);
        }

        [Fact(DisplayName = "The one where a 1kg international parcel costs £12.50")]
        public async Task InternationalParcelAppliesTwoPointFiveMultiplier()
        {
            var request = new ShippingCalculateRequest(1.0m, DistanceZone.International, 0.00m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
            body!.TotalCost.ShouldBe(12.50m);
        }
    }

    public class FreeShipping : ShippingCalculateAcceptanceTests
    {
        public FreeShipping(WebApplicationFactory<Program> factory) : base(factory)
        {
        }

        [Fact(DisplayName = "The one where a domestic order of £75.00 or more gets free shipping")]
        public async Task DomesticOrderOver75GetsFreeShipping()
        {
            var request = new ShippingCalculateRequest(3.0m, DistanceZone.Domestic, 75.00m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
            body!.Breakdown.FreeShippingApplied.ShouldBeTrue();
            body.TotalCost.ShouldBe(0.00m);
        }

        [Fact(DisplayName = "The one where a domestic order under £75.00 pays the zoned rate")]
        public async Task DomesticOrderUnder75PaysZonedRate()
        {
            var request = new ShippingCalculateRequest(3.0m, DistanceZone.Domestic, 74.99m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
            body!.Breakdown.FreeShippingApplied.ShouldBeFalse();
            body.TotalCost.ShouldBe(body.Breakdown.ZonedRate);
        }
    }

    public class Validation : ShippingCalculateAcceptanceTests
    {
        public Validation(WebApplicationFactory<Program> factory) : base(factory)
        {
        }

        [Fact(DisplayName = "The one where negative weight is rejected with a 400 and a validation error body")]
        public async Task NegativeWeightReturns400()
        {
            var request = new ShippingCalculateRequest(-1.0m, DistanceZone.Domestic, 10.00m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            body!.Code.ShouldBe("INVALID_WEIGHT");
        }

        [Fact(DisplayName = "The one where negative order total is rejected with a 400 and a validation error body")]
        public async Task NegativeOrderTotalReturns400()
        {
            var request = new ShippingCalculateRequest(1.0m, DistanceZone.Domestic, -0.01m);

            var response = await _client.PostAsJsonAsync("/api/shipping/calculate", request);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            body!.Code.ShouldBe("INVALID_ORDER_TOTAL");
        }
    }
}
