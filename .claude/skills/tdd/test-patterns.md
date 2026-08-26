# Test patterns by architecture layer (.NET 10 / ASP.NET Core / xUnit)

Reference for writing the RED test at the right level. This service runs on
**.NET 10, ASP.NET Core, xUnit 2.9, Shouldly 4.3** (see the `.csproj` files
under `tests/`).

> The examples below are pulled directly from this repo's shipping cost
> calculator: `POST /api/shipping/calculate` takes
> `{ weightKg, zone, orderTotal }` and returns
> `{ totalCost, breakdown: { baseRate, zoneMultiplier, zonedRate, freeShippingApplied } }`.
> Two of its rules run through the examples: **the base rate is looked up
> from the parcel's weight tier**, and **a domestic order of £75.00 or more
> gets free shipping**. Sections 2 and 4 below are copied verbatim (or
> lightly trimmed) from the real test files —
> `tests/ShippingCalculator.Domain.Tests/Services/ShippingCostServiceTests.cs`
> and `tests/ShippingCalculator.Api.Tests/ShippingCalculateAcceptanceTests.cs`
> — so they're proven-correct, not invented. Sections 1 and 3 are compiled
> and run against this repo but aren't literally present in it today; each
> says so. Swap in your own types and rules when this worked example is
> deleted — the patterns (nesting, `Result<T>` assertions,
> `WebApplicationFactory`) carry over.

## Pick the tier deliberately

Push each rule to the **lowest tier that can prove it**. Most rules are
plain logic and belong in a service test with no hosted ASP.NET Core context
(milliseconds). Reach for the acceptance tier only to prove wiring you can't
prove otherwise (HTTP routing, JSON (de)serialization, status codes).

| Layer under test | Test style | Hosts ASP.NET Core? | Proves |
| --- | --- | --- | --- |
| Value object / record | Plain xUnit, no framework | No | Pure data, equality, and small derivations |
| Service / business logic | Plain xUnit + Shouldly | No | The calculation/orchestration rules, and both branches of every `Result<TValue, Error>` |
| Controller (web slice) | `WebApplicationFactory` + a hand-written stub swapped in via `ConfigureTestServices` | Web host, real service replaced by a stub | Routing, (de)serialization, status codes — not business logic |
| Acceptance / full HTTP cycle | `WebApplicationFactory<Program>` + `HttpClient`, real service wired in | Full app, in-memory server | The whole wired path end to end, no network |
| Repository / outbound adapter | Plain xUnit + **Testcontainers** — a real engine in a throwaway container | No | That the *real* implementation works against the *real* external system, not against a fake |

That last row is the one people skip, and skipping it is how a codebase ends
up with a fully green suite and an outbound adapter nobody has ever run. See
"Repository / outbound adapter tier" below.

Don't *exhaustively* re-test a rule at the acceptance tier: keep one
representative, business-readable example there as living documentation, and
push the full boundary/edge enumeration down to the service tier. This
repo's acceptance tests do keep a short `[Theory]` for the weight-tier table
too, since the table is short and cheap to re-run at every tier — overlap on
the headline example is by design; replicating a long table across tiers is
the waste to avoid.

## Dependencies (already in the `.csproj` files)

No extra test packages are needed for the value-object and service tiers —
`tests/ShippingCalculator.Domain.Tests/ShippingCalculator.Domain.Tests.csproj`
references only `xunit`, `Shouldly`, and the Domain project. The acceptance
and controller-slice tiers need one more package, already present in
`tests/ShippingCalculator.Api.Tests/ShippingCalculator.Api.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
```

This pulls in `WebApplicationFactory<TEntryPoint>` (and, transitively,
`Microsoft.AspNetCore.TestHost` for `ConfigureTestServices`, used in the
controller-slice example below). `Program.cs` exposes a
`public partial class Program` at the bottom of the file specifically so the
test project can reference it as `WebApplicationFactory<Program>`'s generic
argument.

## 1. Value object / record — plain xUnit, no framework

This repo's weight-tier classification (`DetermineWeightTier`) is a private
method on `ShippingCostService`, so it's proven through the service tier
below rather than as a standalone value-object test — there's no public
classification type to test in isolation today. What *is* a plain value
object is `ShippingCostBreakdown`, a `record` in
`src/ShippingCalculator.Domain/Models/ShippingCostBreakdown.cs`. Records get
value equality for free; a value-object test proves that (and, for a type
with its own logic, would prove that logic at its boundary):

```csharp
using Shouldly;
using ShippingCalculator.Domain.Models;

namespace ShippingCalculator.Domain.Tests.Models;

public class ShippingCostBreakdownTests
{
    [Fact(DisplayName = "The one where two breakdowns with the same values are equal")]
    public void RecordsWithEqualValuesAreEqual()
    {
        var a = new ShippingCostBreakdown(10.00m, 1.5m, 15.00m, false);
        var b = new ShippingCostBreakdown(10.00m, 1.5m, 15.00m, false);

        a.ShouldBe(b);
    }

    [Fact(DisplayName = "The one where a different value breaks equality")]
    public void RecordsWithDifferentValuesAreNotEqual()
    {
        var a = new ShippingCostBreakdown(10.00m, 1.5m, 15.00m, false);
        var b = new ShippingCostBreakdown(10.00m, 1.5m, 15.00m, true);

        a.ShouldNotBe(b);
    }
}
```

If your domain has a public value type with its own classification or
derivation logic (a `Money` type, a postcode validator, a discount-tier
enum-from-value), test it exactly like this but call its own method and
assert the boundary — the value where the classification flips — the same
way the service test below proves the weight-tier boundary.

## 2. Service — plain xUnit, both branches of `Result<T>` (the workhorse tier)

Construct the service directly — no hosted context, no DI container. Most
rules live and are driven here. Verbatim from
`tests/ShippingCalculator.Domain.Tests/Services/ShippingCostServiceTests.cs`:

Success case — assert `IsSuccess` **and** the value, not just a boolean:

```csharp
using Shouldly;
using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Services;

namespace ShippingCalculator.Domain.Tests.Services;

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
    }
}
```

Failure case — assert `IsFailure` **and** the error code, never by catching
an exception (the service doesn't throw for an expected validation failure):

```csharp
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
}
```

Reading `.Value` on a failed `Result` (or `.Error` on a successful one) is a
programmer bug, not a domain failure, so `Result<TValue, TError>` throws
`InvalidOperationException` for it — the real test file proves this
directly:

```csharp
[Fact(DisplayName = "Reading Value on a failed result throws — that is a programmer bug, not a domain failure")]
public void ReadingValueOnFailureThrows()
{
    var result = _service.Calculate(new ShippingCostRequest(-1.0m, DistanceZone.Domestic, 0.00m));

    Should.Throw<InvalidOperationException>(() => result.Value);
}
```

Money assertions: `decimal` has value equality, so `.ShouldBe(10.00m)` is
always correct — there's no `compareTo()`-style caveat to work around the
way there is with Java's `BigDecimal`.

**When the service has a collaborator** that needs isolating, write a small
hand-rolled stub implementing its interface (this repo has no mocking
library on the classpath — none of the current services have collaborators
that need one). Constructor-inject the stub and assert against the service
under test, still with no hosted context.

## 3. Controller (web slice) — `WebApplicationFactory` + a stub service

Not used in this repo today — the acceptance tier below is cheap enough
that isolating routing from the real service hasn't been necessary. Reach
for this tier only when the real service is slow or has side effects you
want to keep out of a routing/serialization test. Swap the real
`IShippingCostService` for a stub via `ConfigureTestServices`; this proves
the controller maps a `Result` to the right status code and JSON shape, not
the calculation:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShippingCalculator.Api.Models;
using ShippingCalculator.Domain.Models;
using ShippingCalculator.Domain.Services;
using ShippingCalculator.Domain.Shared;

namespace ShippingCalculator.Api.Tests.Controllers;

public class ShippingControllerSliceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ShippingControllerSliceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed class StubShippingCostService : IShippingCostService
    {
        public Result<ShippingCostResult, Error> Calculate(ShippingCostRequest request) =>
            Result<ShippingCostResult, Error>.Success(
                new ShippingCostResult(15.00m, new ShippingCostBreakdown(10.00m, 1.5m, 15.00m, false)));
    }

    [Fact(DisplayName = "The one where the endpoint returns the service's result as JSON")]
    public async Task ReturnsCalculationResultAsJson()
    {
        var client = _factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.AddScoped<IShippingCostService, StubShippingCostService>()))
            .CreateClient();

        var response = await client.PostAsJsonAsync("/api/shipping/calculate",
            new ShippingCalculateRequest(3.2m, DistanceZone.European, 50.00m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ShippingCalculateResponse>();
        body!.TotalCost.ShouldBe(15.00m);
    }
}
```

## 4. Acceptance test — `WebApplicationFactory<Program>` + `HttpClient`, structured with nested classes

**One test class per story/feature** in the `*.Api.Tests` project (flat —
not nested in an `Acceptance/` subfolder), named `<Feature>AcceptanceTests`.
Loads the **whole** application with the real service wired in and drives it
through `HttpClient` — no network socket, no mocks. Assert the HTTP contract
here, not the logic the service test already covers.

xUnit has no `[Nested]` attribute, so mirror the spec with **one nested
`public class` per rule, inheriting the outer class** so it shares the
outer class's `WebApplicationFactory<Program>` fixture and
constructor-injected `HttpClient`:

- The outer class = the feature (documented in a doc comment — xUnit has no
  class-level display name the way `@DisplayName` does).
- One nested `public class` per rule, named after the rule.
- One `[Fact]`/`[Theory]` per example/counter-example, each a `"The one
  where…"` `DisplayName`.

Verbatim (trimmed) from
`tests/ShippingCalculator.Api.Tests/ShippingCalculateAcceptanceTests.cs`:

```csharp
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
    }
}
```

In the test-run report this reads as **`Validation` › "The one where
negative weight is rejected with a 400 and a validation error body"** — each
nested class a group, each example a line under it.

## 5. Repository / outbound adapter — Testcontainers against a real engine

This starter's worked example has no persistence and no outbound
dependency, so there's nothing to test here *yet*. Read this section the
moment your project grows one — a database, a message broker, a cache, a
third-party HTTP API.

**Writing the real implementation is an ordinary `/tdd` cycle.** There is no
separate skill, command, or workflow for it: you pick this tier in AIM, write
a RED test against a real engine, and write the real adapter in GREEN. The
only thing that changes is what the test talks to.

**Why this tier is not optional.** Every other tier substitutes a fake for
your outbound port, which is exactly right — it keeps the inner loop fast and
deterministic. But it also means a repository or adapter can be "fully
tested" while never once having run against the thing it's an adapter *for*.
A fake can't reject your SQL, can't enforce a unique constraint, can't
disagree with your mapping. This tier is where the real system gets its say.

**Pick the engine deliberately — highest fidelity you can actually run:**

| Option | Real SQL engine? | Needs Docker? | Use when |
| --- | --- | --- | --- |
| **Testcontainers** | Yes — the actual engine you deploy on | Yes | Default when Docker is available. Highest fidelity: your real dialect, your real constraints, your real migrations. |
| **SQLite in-memory** (`Microsoft.Data.Sqlite`, `:memory:`) | Yes, but a different dialect | No | Docker isn't available, or you want a fast in-process tier. Catches mapping and constraint mistakes; won't catch provider-specific SQL. |
| **EF Core InMemory provider** | **No** — not relational at all | No | Almost never. It doesn't run SQL, ignores constraints, and will accept a schema the real database rejects. |

Don't let the third row masquerade as the second: the EF Core InMemory
provider is a dictionary with a LINQ front end, not a database. If Docker is
out of reach, reach for SQLite in-memory, not InMemory.

The Testcontainers version — a real engine in a throwaway container, started
and disposed by the test itself, no shared local install and no
developer-machine drift:

```xml
<PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
```

```csharp
using Testcontainers.PostgreSql;

namespace ShippingCalculator.Api.Tests.Repositories;

public class ShipmentRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    private ShipmentDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _dbContext = new ShipmentDbContext(
            new DbContextOptionsBuilder<ShipmentDbContext>()
                .UseNpgsql(_database.GetConnectionString())
                .Options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact(DisplayName = "The one where a saved shipment is read back with its cost intact")]
    public async Task SavedShipmentIsReadBackWithItsCostIntact()
    {
        var repository = new ShipmentRepository(_dbContext);
        await repository.SaveAsync(new Shipment("SHIP-1", 15.00m));

        var found = await repository.FindByReferenceAsync("SHIP-1");

        found.ShouldNotBeNull();
        found.Cost.ShouldBe(15.00m);   // decimal, not double — the DB column type matters here
    }
}
```

Assert the things only a real engine can prove: that `decimal` survives the
round trip at the right scale, that a unique constraint actually rejects a
duplicate, that a migration applies cleanly, that your mapping reads back
what it wrote. Don't re-test business rules here — those belong at the
service tier, which already proved them without a container.

**Whichever engine you pick, the tier is not optional.** The choice above is
about fidelity and what your machine can run; it is not permission to skip
the tier and let a fake stand in for the real implementation. A SQLite-backed
repository test that actually runs your queries is worth far more than no
repository test at all.

**The genuine exception: a dependency with no containerized equivalent** — a
vendor SaaS API, a cloud provider's management plane. Only there, fall back
to a test that reads credentials **exclusively from environment variables**
(the developer's shell locally, repository/environment secrets in CI — never
`appsettings.json`, never hard-coded), and **skips cleanly with an
explanatory message** when they're absent, so a contributor without those
credentials still sees a green suite:

```csharp
[Fact(DisplayName = "The one where the real vendor API confirms a known account")]
public async Task RealVendorApiConfirmsAKnownAccount()
{
    var apiKey = Environment.GetEnvironmentVariable("VENDOR_API_KEY");
    if (apiKey is null)
    {
        _output.WriteLine("SKIPPED: set VENDOR_API_KEY to run this against the real vendor API.");
        return;
    }
    // ...exercise the real adapter...
}
```

That environment-variable read belongs in the **test**, to decide whether to
skip. The adapter itself takes its configuration the normal ASP.NET Core way
— constructor-injected `IOptions<T>` / `IConfiguration`, bound from
environment variables by the framework's built-in provider — never a direct
`Environment.GetEnvironmentVariable` call in production code.

## Data-driven rules → one `[Theory]`, not one `[Fact]` per row

When a rule is a table — a rate table, a set of boundary values, an input →
output mapping — drive it with a single `[Theory]` and one `[InlineData]`
row per case, at whatever tier proves the rule. Don't copy-paste a `[Fact]`
per row. The spec's table becomes the data source; counter-examples are
just more rows. See the `WeightTierBaseRates` example in section 2 above, or
`.claude/skills/accept/SKILL.md` → "Data-driven rules" for the acceptance-tier
version.

**Fits the inner loop:** a new boundary is still the next RED — add a *row*
to the existing `[Theory]`, don't spawn a new `[Fact]`. The cycle stays
one-case-at-a-time; the rule converges as a single parameterized test. For
tables longer than ~6 rows, or driven by existing constants/enums, prefer
`[MemberData]` over a wall of `[InlineData]` attributes.

## Naming & convention reminders

- Method names state the rule (`DomesticOrderOver75GetsFreeShipping`), never
  a number (`TestCalculate3`).
- `DisplayName` is a plain-language, Example-Mapping line: `"The one where a
  3.2kg European parcel costs £15.00"`.
- Acceptance classes are suffixed `AcceptanceTests`; one class per
  story/feature in the `*.Api.Tests` project, a nested class per rule, one
  `[Fact]`/`[Theory]` per example/counter-example.
- Money: `decimal`, scale 2, `MidpointRounding.AwayFromZero`, assert with
  `.ShouldBe(...)` — `decimal` equality is already value-based, no
  `compareTo()`-style caveat.
- Data-driven rules (rate tables, boundary sets, input → output maps) are
  ONE `[Theory]` with a case per row — never one copy-pasted `[Fact]` per
  row. Give the `[Theory]` itself a `DisplayName` to keep the "The one
  where…" voice.
