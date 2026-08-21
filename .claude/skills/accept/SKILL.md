---
name: accept
model: claude-sonnet-5
allowed-tools: Read, Write, Edit, Bash
description: >-
  Write a failing acceptance test for the NEXT spec rule (Step 2 of the
  development process). Use after a spec in docs/specs/ is finalised and before
  any production code is written for that rule. One rule at a time — the test
  must fail for the right reason.
argument-hint: "<rule name> @docs/specs/<feature>.md"
---

Write a failing acceptance test for: $ARGUMENTS

Read CLAUDE.md for project conventions before writing anything.
Re-read the spec file to understand the full rule, its examples, and its counter-examples.

## Structure

One test class per story/feature, named `<Feature>AcceptanceTests`, in the
`*.Api.Tests` project (e.g. `tests/ShippingCalculator.Api.Tests/`), flat — not
nested in an `Acceptance/` subfolder.

xUnit has no `[Nested]` attribute, so group by rule with **one nested `public
class` per rule, inheriting the outer class** (so it shares the outer class's
`WebApplicationFactory<Program>` fixture and constructor-injected `HttpClient`).
Name the nested class after the rule; give each `[Fact]`/`[Theory]` a
`DisplayName` with the spec's exact business language, so the test-run report
reads like the Example Map:

- Outer class: the feature name (in a doc comment, since xUnit has no class-level
  display name the way `@DisplayName` does)
- Nested class: the rule
- `DisplayName`: the "The one where…" text from the spec (counter-examples too)

One `[Fact]` per example AND per counter-example from the spec — **unless the
rule is data-driven** (see below).

See `.claude/skills/tdd/SKILL.md` → "Acceptance test" for the full worked template.

## How to test

Test through the REST API. Use `Microsoft.AspNetCore.Mvc.Testing`'s
`WebApplicationFactory<Program>` to host the real app in-memory, and drive it
with the plain `HttpClient` it hands you — NOT a hand-rolled `HttpClient`
pointed at a real port, and NOT a mocked service.

Send real HTTP requests. Assert real HTTP responses.
NEVER call services or domain objects directly — this is an acceptance test, not a unit test.

Skeleton (replace the placeholder `<Feature>` / `<Rule>` / endpoint with the spec's):

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace <RootNamespace>.Api.Tests;

/// <summary><Feature name, in the spec's words>.</summary>
public class <Feature>AcceptanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly HttpClient _client;

    public <Feature>AcceptanceTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public class <Rule> : <Feature>AcceptanceTests
    {
        public <Rule>(WebApplicationFactory<Program> factory) : base(factory) { }

        [Fact(DisplayName = "The one where <the example>")]
        public async Task <ExampleMethodName>()
        {
            var response = await _client.PostAsJsonAsync("/api/<resource>", new
            {
                /* ...request fields from the example... */
            });

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<<ResponseType>>();
            body!.<Field>.ShouldBe(<expected>m);
        }
    }
}
```

Assert exact values from the spec examples. `decimal` has value equality, so
`.ShouldBe(1.60m)` is always correct for money — there's no `compareTo()`-style
caveat to work around the way there is with Java's `BigDecimal`. Do NOT compare
a money field as `double` — read it as `decimal` from the response DTO, never
parse it as a floating-point type.

## Data-driven rules → one theory

If the rule's examples are a table of same-shaped rows, use a single `[Theory]`
with `[InlineData(...)]` (or `[MemberData]` for tables longer than ~6 rows, or
rows sourced from existing constants/enums) inside the nested rule class. Keep
the Example-Mapping voice with a `DisplayName` on the `[Theory]` itself and, if
you want the resolved row visible in the test-run report, format the row values
into the method name or a helper.

```csharp
public class UnitPriceTimesQuantity : <Feature>AcceptanceTests
{
    public UnitPriceTimesQuantity(WebApplicationFactory<Program> factory) : base(factory) { }

    [Theory(DisplayName = "The line total is the unit price times the quantity")]
    [InlineData(1, 2.50, 2.50)]
    [InlineData(3, 2.50, 7.50)]
    [InlineData(4, 10.00, 40.00)]
    public async Task LineTotalIsUnitPriceTimesQuantity(int quantity, decimal unitPrice, decimal expectedTotal)
    {
        var response = await _client.PostAsJsonAsync("/api/order-lines", new
        {
            sku = "A1",
            quantity,
            unitPrice
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrderLineResponse>();
        body!.Total.ShouldBe(expectedTotal);
    }
}
```

Include the rows on each side of a boundary (the last row before a
classification flips and the first after), exactly the boundaries you'd
otherwise write as separate `[Fact]`s.

## What NOT to do

Do NOT write production code. The test MUST FAIL.
A passing test means you tested nothing.
Do NOT write tests for all rules at once.
One rule only — the one specified in the arguments.
Do NOT invent examples beyond what the spec provides.
The spec is the contract.
Do NOT use mocks in acceptance tests.
Wire the full stack: controller → service → domain → persistence.

## When you're done

Run the test (`dotnet test --filter "FullyQualifiedName~<TestClass>"`). Confirm it fails for the RIGHT reason:
- Missing endpoint → 404 or compile error (good)
- Wrong value → not yet, the endpoint shouldn't exist
- Test passes → something is wrong, investigate

Report: which rule you tested, how many examples, and the failure reason.

STOP. Do not proceed to implementation.
