# ASP.NET Core SDD Starter

A starter for building ASP.NET Core services with a Spec-Driven Development (SDD) workflow. It ships with a working ASP.NET Core / .NET solution, structured for a `.claude/` toolchain (hooks, agents, commands) that drives a test-first workflow, enforces specs, and guards architecture boundaries.

<!-- ADAPT: This file is the single most important input for Claude — it's read
     before any work. Every section below is a working default for a standard
     ASP.NET Core REST service. Edit the content to match your project; the
     HTML comments tell you what to change in each section. A worked example
     (a shipping cost calculator) sits at the bottom — delete it once your own
     sections are accurate. -->

## Project Overview

An ASP.NET Core REST service built and tested with the .NET SDK.

- **Build tool:** .NET SDK (`dotnet`)
- **Framework:** ASP.NET Core on .NET 10 (LTS) — see `ShippingCalculator.sln` and the `.csproj` files for the exact package versions
- **Testing:** xUnit + Shouldly

<!-- ADAPT: Replace this paragraph with one or two sentences describing what
     your service actually does. Keep the build/framework facts in sync with
     the `.csproj` files. -->

## Architecture

Standard ASP.NET Core layered architecture. Keep it simple — controller calls service, service uses models:

- **Controllers/** — REST endpoints (`[ApiController]`). Receives requests, delegates to a service, returns responses. No business logic.
- **Services/** — All business logic and orchestration. Testable without a web host. Keeps HTTP and persistence concerns out.
- **Models/** — Domain objects, request/response DTOs, enums, value objects. Immutable where practical (`record`); no business logic.
- **Repositories/** — Data access. Only present when the project uses a database.

Layer rules: controllers never contain business logic; services never import controller-layer or web types (`Microsoft.AspNetCore.Mvc`, etc.); models hold data, not logic. The `architecture-guardian` agent enforces these boundaries.

<!-- ADAPT: If you use a different style (hexagonal: ports/adapters/domain,
     clean architecture: entities/usecases/interfaces, modular monolith,
     microservices), replace the layers and rules above to describe it, and
     update .claude/agents/architecture-guardian.md to match. -->

## Processing Order

When a feature applies a fixed sequence of steps, the code and the tests must follow that exact order, and the order is documented here so it can't drift.

<!-- ADAPT: List your domain's processing/calculation chains here, in order —
     e.g. "Price: base price → discount → tax → total" or
     "Request: validate → enrich → persist → notify". Delete this section if
     no feature in your domain has an order-dependent chain. -->

## Monetary / Numeric Precision

For money and other exact decimal values:

- Use `decimal` — never `double` or `float`.
- Scale: 2 decimal places. Rounding: `Math.Round(value, 2, MidpointRounding.AwayFromZero)` (HALF_UP equivalent).
- Compare with plain `==` / assertion equality. Unlike Java's `BigDecimal`, C#'s `decimal` equality already compares by value regardless of trailing-zero scale (`1.50m == 1.5m` is `true`), so there is no `compareTo`-style caveat to worry about here.
- Assert exact values in tests — no floating-point tolerance.

<!-- ADAPT: Adjust the scale and rounding mode to your domain's rules. Delete
     this whole section if the project handles no exact decimal values. -->

## Error Handling

Expected business/validation failures are modelled with a small, dependency-free `Result<TValue, TError>` type (`src/ShippingCalculator.Domain/Shared/Result.cs`), not exceptions:

- Domain/service methods that can fail for expected reasons return `Result<TValue, Error>`, where `Error` is a `record Error(string Code, string Message)`.
- `Result` exposes `IsSuccess` / `IsFailure`, `Value` / `Error` accessors (throwing only on programmer misuse — e.g. reading `.Value` on a failure — which is a bug, not a domain failure), and a `Match(onSuccess, onFailure)` method.
- Controllers call `.Match(...)` on the returned `Result` to pick the HTTP status: success → `200`, failure → `400` (or another explicit status). This replaces exception-to-HTTP-status mapping — there is no exception-handler middleware for domain errors.
- Reserve real exceptions (`throw`) for truly exceptional/infrastructure conditions — a failed database connection, a bug — never for expected business rule violations.

<!-- ADAPT: If your domain needs more failure shapes (e.g. multiple errors per
     request, different HTTP statuses per error code), extend `Error`/`Result`
     usage here — keep the type itself dependency-free. -->

## API Design

REST conventions for this service:

- Endpoints accept and return JSON (`Content-Type: application/json`).
- Responses return a breakdown of intermediate steps where relevant, so every stage is visible and testable rather than just a final value.
- Status codes are explicit: `200` success, `400` validation error, `401`/`403` auth, `404` not found.

```
POST /api/<resource>
Content-Type: application/json

{ ...request fields... }

Response 200:
{ ...response fields, including a breakdown of intermediate steps... }
```

<!-- ADAPT: Replace the endpoint, the request/response shapes, and the status
     codes with your real API. Include at least one concrete example request
     and response — the worked example at the bottom of this file shows the
     level of detail that helps. -->

## Testing Conventions

Two tiers of tests:

- **Acceptance tests** (`tests/ShippingCalculator.Api.Tests/`): `WebApplicationFactory<Program>` + `HttpClient`. One test class per feature, suffixed `AcceptanceTests`. Test the full HTTP request/response cycle.
- **Service / unit tests** (`tests/ShippingCalculator.Domain.Tests/`): Plain xUnit, no `WebApplicationFactory`. Test business logic directly.

Conventions:

- Test method names describe the business rule (`DomesticOrderOver75GetsFreeShipping`), not a number (`Calculate_Test3`).
- Group examples for a rule in a nested test class named after the rule, with a name that reads as the plain-language, Example-Mapping-style description, e.g. `FreeShipping`, with methods like `DomesticOrderOver75GetsFreeShipping` — xUnit supports nested test classes.
- Acceptance tests verify the HTTP contract; service tests verify business logic. Don't duplicate the same assertions across both tiers.
- Run the suite with `dotnet test`. The `/accept` and `/tdd` workflows run it for you as part of each cycle, so you see each test go red and green yourself.

<!-- ADAPT: Change the test directories, the build command, and the example
     names if your conventions differ. Keep the two-tier structure — the
     agents and commands assume it. -->

## Spec Files

Business rules live in `docs/specs/` as markdown, one file per feature (`<feature>.specs.md`). Every rule in a spec has at least one acceptance test. Use `/discover` to turn a feature idea into a spec, then `/accept` and `/tdd` to implement it.

<!-- ADAPT: Change the spec directory if it isn't docs/specs/. The
     one-rule-one-test invariant is enforced by the spec-compliance agent —
     keep it. -->

## API Documentation (Swagger/OpenAPI)

Swashbuckle.AspNetCore is included in `src/ShippingCalculator.Api/ShippingCalculator.Api.csproj`. When the app runs, Swagger UI is served at `/swagger` (this replaces the Java original's `/swagger-ui.html` path), generated automatically from the controllers. Add `<summary>` / `[ProducesResponseType]` annotations for richer descriptions.

<!-- ADAPT: Remove this section and the Swashbuckle.AspNetCore dependency if
     the project doesn't expose a REST API. -->

## Security

No authentication is configured yet. Adding it locks down all endpoints immediately — every existing test returns 401 until authentication/authorization middleware is configured. When you add auth, exclude Swagger paths if you want docs to stay public, and update existing acceptance tests to send credentials.

<!-- ADAPT: Document your chosen mechanism (API key header, JWT, OAuth2),
     the roles, and the rule for each (no creds → 401, wrong role → 403, etc.)
     once you enable it. Delete this section if the service is unauthenticated. -->

## The `.claude/` Toolchain

The reusable part of the starter — works for any domain:

- **`.claude/settings.json`** — One hook: a file guard (PreToolUse) that blocks edits to sensitive files. Tests are run by the `/accept` and `/tdd` workflows, not by hooks, so the red/green steps of the cycle stay visible.
- **`.claude/hooks/protect-files.sh`** — Blocks edits to sensitive files via `PROTECTED_PATTERNS`.
- **`.claude/commands/`** — `/discover` (rule → example → counter-example → edge cases → questions), `/accept` (acceptance test against the real endpoint), `/tdd` (failing test → minimum code → verify → refactor).
- **`.claude/agents/`** — `spec-compliance` (specs have tests, precision, feature interactions, API contract) and `architecture-guardian` (layer boundaries).

<!-- ADAPT: Change the test command in the /accept and /tdd workflows if you
     don't use the dotnet CLI. Add your sensitive files to protect-files.sh.
     Update the domain-specific content in the command and agent files. Keep
     the .claude/ structure, the hook exit-code convention (exit 2 to block),
     the $ARGUMENTS placeholder in commands, and docs/specs/. -->

---

<!-- ADAPT: Everything below is the shipping cost calculator that ships with
     this starter as a reference. It shows the level of detail that helps
     Claude. Delete it once the sections above describe your own domain. -->

## Worked Example — Shipping Cost Calculator

An ASP.NET Core REST service that calculates shipping costs based on parcel weight, destination zone, and order value.

- **Architecture:** `Controllers/` (REST, no logic; `ShippingCalculator.Api`) → `Services/` (`ShippingCostService`, pure calculation, no DB; `ShippingCalculator.Domain`) → `Models/` (`ShippingCostRequest`, `ShippingCostResult`, `ShippingCostBreakdown`, enums `WeightTier`, `DistanceZone`, all in `ShippingCalculator.Domain`; request/response DTOs `ShippingCalculateRequest`, `ShippingCalculateResponse`, `ShippingBreakdownResponse`, `ErrorResponse` in `ShippingCalculator.Api`).
- **Processing order:** (1) base rate from weight tier → (2) zone multiplier (domestic ×1.0, European ×1.5, international ×2.5) → (3) free-shipping check (domestic order total ≥ £75.00 → £0.00). The order matters; every spec and test follows it.
- **Weight tiers and base rates** (invented for this template — the Java original ships no implementation to port exact numbers from; kept internally consistent across code, tests, and this document):

  | Weight (kg)   | Base rate |
  |---------------|-----------|
  | 0 < w ≤ 1     | £5.00     |
  | 1 < w ≤ 5     | £10.00    |
  | 5 < w ≤ 10    | £18.00    |
  | 10 < w ≤ 20   | £30.00    |
  | w > 20        | £45.00    |

- **Money:** `decimal`, scale 2, `MidpointRounding.AwayFromZero`, compare with `==`.
- **Error handling:** invalid input (non-positive weight, negative order total) returns `Result<ShippingCostResult, Error>.Failure(...)` from the service, which the controller maps to `400` with an `{ code, message }` body — no exceptions thrown for these cases.
- **API:** `POST /api/shipping/calculate` taking `{ weightKg, zone, orderTotal }` (`zone` is one of `"Domestic"`, `"European"`, `"International"`) and returning `{ totalCost, breakdown: { baseRate, zoneMultiplier, zonedRate, freeShippingApplied } }` on success, or `400` with `{ code, message }` on validation failure.

  Example:

  ```
  POST /api/shipping/calculate
  Content-Type: application/json

  { "weightKg": 3.2, "zone": "European", "orderTotal": 50.00 }

  Response 200:
  { "totalCost": 15.00, "breakdown": { "baseRate": 10.00, "zoneMultiplier": 1.5, "zonedRate": 15.00, "freeShippingApplied": false } }
  ```

- **Spec files:** `weight-tiers.specs.md`, `distance-zones.specs.md`, `free-shipping.specs.md`, `validation.specs.md` (once `.claude/` and `docs/specs/` are scaffolded in a later phase).
- **Security:** not yet configured — see the *Security* section above.
