---
paths:
  - "tests/**"
---

You are editing test code. Tests are executable specifications.

- Prove each rule at the lowest tier that can: service / value-object tests
  (plain xUnit + Shouldly, no hosted context) for pure logic; `WebApplicationFactory<Program>`
  + `HttpClient` acceptance tests only for the HTTP contract. Don't repeat the
  same assertion across both tiers. (Per-tier compiled examples live in the tdd
  skill's `test-patterns.md`.)
- A **repository or other outbound adapter** gets one more tier, and it is not
  optional: prove the REAL implementation against a REAL SQL engine —
  Testcontainers when Docker is available, SQLite in-memory when it isn't,
  never the EF Core InMemory provider (which runs no SQL and ignores
  constraints). Every other tier substitutes a fake for that port — which is
  correct, and which is also why an adapter can look fully tested while never
  once having run against the thing it adapts. Writing that real
  implementation is an ordinary `/tdd` cycle at this tier, never a separate
  workflow. See `test-patterns.md` → "Repository / outbound adapter".
- Placement: acceptance tests in the `*.Api.Tests` project (e.g.
  `tests/ShippingCalculator.Api.Tests/`), flat — one class per feature, not
  nested in an `Acceptance/` subfolder; service/unit tests in the
  `*.Domain.Tests` project (e.g. `tests/ShippingCalculator.Domain.Tests/`), in
  a subfolder mirroring the layer under test (e.g. `Services/`).
- Name methods after the business rule (`DomesticOrderOver75GetsFreeShipping`),
  never `TestCalculate3`. Use `[Fact(DisplayName = "...")]` / `[Theory(DisplayName = "...")]`
  with plain-language, Example-Mapping descriptions: `"The one where a 3kg
  European parcel costs £7.49"`.
- Money assertions: `decimal` has value equality, so a direct `.ShouldBe(...)`
  is always correct — unlike Java's `BigDecimal`, there's no `compareTo()`
  footgun to work around. Assert exact values; no floating-point tolerance, and
  never use `double` for a monetary expected value.
- Never modify an existing test to make production code pass — fix the production
  code instead.
