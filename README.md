# ASP.NET Core SDD Starter

A starter for building ASP.NET Core services using a **Spec-Driven Development (SDD)** workflow with Claude Code. It bundles a working ASP.NET Core / .NET 10 solution together with a `.claude/` toolchain — skills, agents, and hooks — that turns business rules into specs, specs into acceptance tests, and tests into code, with the test suite enforced automatically on every change.

The project ships with a small example domain (a shipping cost calculator) so everything is runnable out of the box. See [Adapting This Starter](#adapting-this-starter) to make it your own.

## Requirements

- **.NET 10 SDK** (LTS). Check with `dotnet --list-sdks`. A `global.json` in the repo root pins the exact SDK version (`10.0.301`) so `dotnet` commands resolve to it even if newer/preview SDKs are also installed.
- No wrapper needed — the .NET SDK includes the build/test/run tooling directly.
- Internet access on first build so NuGet can fetch dependencies.

## Build

```bash
dotnet build              # compile all projects
dotnet build -c Release   # release configuration
```

Output assemblies are written to each project's `bin/` folder (e.g. `src/ShippingCalculator.Api/bin/Debug/net10.0/`).

## Test

```bash
dotnet test                                                # run the full suite
dotnet test --filter FullyQualifiedName~ShippingCostService # a single test class
dotnet test tests/ShippingCalculator.Api.Tests              # only acceptance tests
dotnet test -v detailed                                     # verbose output
```

Tests come in two tiers (see `CLAUDE.md` → *Testing Conventions*):

- **Acceptance tests** — `tests/ShippingCalculator.Api.Tests/`, `WebApplicationFactory<Program>` + `HttpClient`, exercising the full HTTP cycle.
- **Service / unit tests** — `tests/ShippingCalculator.Domain.Tests/`, plain xUnit, no web host.

Testing stack: **xUnit** and **Shouldly**.

## Run

```bash
dotnet run --project src/ShippingCalculator.Api
```

The service starts on **http://localhost:5016** by default (see the `http` profile in `src/ShippingCalculator.Api/Properties/launchSettings.json`; the exact port is also printed on startup).

Example request against the bundled shipping calculator:

```bash
curl -X POST http://localhost:5016/api/shipping/calculate \
  -H 'Content-Type: application/json' \
  -d '{ "weightKg": 3.2, "zone": "European", "orderTotal": 50.00 }'
```

To change the port or other settings, edit `src/ShippingCalculator.Api/appsettings.json` or pass it at runtime: `dotnet run --project src/ShippingCalculator.Api --urls http://localhost:9090`.

### API documentation (Swagger UI)

With the app running, interactive API docs are at **http://localhost:5016/swagger**, generated automatically from the controllers by Swashbuckle.AspNetCore. The raw OpenAPI spec is at `/swagger/v1/swagger.json`.

## The SDD Workflow

Claude Code drives features through three steps, each backed by a skill you invoke by name:

1. **`/discover`** — Turn a feature idea into a spec using Example Mapping (rule → example → counter-example → edge cases → open questions). Saves a draft spec to `docs/specs/<feature>.specs.md`.
2. **`/accept`** — Write a failing acceptance test for the next rule in a spec, hitting the real endpoint. One rule at a time.
3. **`/tdd`** — Run one TDD inner-loop cycle (RED → GREEN → REFACTOR) to drive that test to green with the minimum code.

Four review agents enforce quality on demand:

- **`architecture-guardian`** — checks that code respects the layer boundaries (controllers stay thin, services hold the logic, models stay data-only).
- **`spec-compliance`** — checks that every spec rule has a test, that precision/ordering rules hold, and that the API contract matches.
- **`config-auditor`** — audits the `.claude/` setup itself for token efficiency and trigger reliability.
- **`mutation-analyst`** — runs Stryker.NET and reports which surviving mutants indicate weak assertions.

One hook (configured in `.claude/settings.json`) runs automatically:

- **PreToolUse** — `protect-files.sh` blocks edits to protected files (prod config, secrets, CI). Tests are run explicitly by the `/accept` and `/tdd` skills as part of each cycle, not by a hook, so the red/green steps stay visible turn by turn.

The `/quality-check` command chains spec-compliance, architecture-guardian, and (if a Stryker report exists) mutation-analyst into one consolidated report.

## Project Layout

```
ShippingCalculator.sln                          Solution file
global.json                                      Pins the .NET SDK version
src/ShippingCalculator.Api/                      Controllers/, Models/ (request/response DTOs), Program.cs
src/ShippingCalculator.Domain/                   Services/, Models/ (domain value objects/enums), Shared/Result.cs
tests/ShippingCalculator.Api.Tests/              Acceptance tests (WebApplicationFactory + HttpClient)
tests/ShippingCalculator.Domain.Tests/           Service/unit tests (plain xUnit)
docs/specs/                                      Business rules, one .specs.md file per feature
docs/api/                                        OpenAPI contracts (optional; the review skill checks against them if present)
CLAUDE.md                                        Project context Claude reads before any work
.claude/skills/                                  bootstrap, discover, accept, tdd, review, commit-summary, claudius
.claude/agents/                                  architecture-guardian, spec-compliance, config-auditor, mutation-analyst
.claude/rules/                                   Path-scoped conventions for Controllers/, Services/, Models/, tests/
.claude/commands/                                quality-check (chains the review agents)
.claude/hooks/                                   protect-files.sh (file guard)
.claude/settings.json                            Hook + permission wiring
```

## Adapting This Starter

This repo is a template. The `.claude/` toolchain works for any ASP.NET Core / .NET project — you replace the example domain with your own.

### The fast path: `/bootstrap`

Run **`/bootstrap`** right after cloning and Claude Code walks the whole
adaptation below for you, interactively: project name, domain, architecture
(pass `--architecture hexagonal` for ports & adapters instead of the default
layered style), processing order, monetary precision, API shape, security,
and protected files — then renames the solution and deletes the worked
example's documentation once your own sections are accurate.

The same skill also works the other way: copy just `.claude/skills/bootstrap/`
into an **existing** ASP.NET Core project that doesn't have this toolchain yet,
and run `/bootstrap` there. It detects it's being run on an existing codebase,
fetches the rest of the harness from this public repo automatically, and
infers every answer above by reading your code instead of asking — zero
prompts. Check the report it prints at the end for anything it had to guess.

### Doing it by hand

Most files carry `ADAPT` comments pointing at exactly what to change, if you'd
rather not run the skill (or want to understand what it's doing):

1. **`CLAUDE.md`** — the most important file; Claude reads it before doing anything. Each section is a working default with an `<!-- ADAPT -->` comment explaining what to change. Replace the Project Overview, Architecture, API Design, and (if present) Processing Order / Monetary sections with your domain, and delete the *Worked Example* block at the bottom once your sections are accurate.

2. **`.csproj` files** — set the target framework and add the NuGet packages your project needs (e.g. `Microsoft.EntityFrameworkCore` for a database, `FluentValidation` if you want richer request validation on top of the `Result` pattern).

3. **Rename the project** — change the solution and project names, the root namespaces, and move the code from the `ShippingCalculator.*` namespaces to your own.

4. **`.claude/settings.json`** — if you don't use the `dotnet` CLI directly (e.g. a custom build script), change the test command referenced in the `/accept` and `/tdd` skills. The hook wiring itself stays the same.

5. **`.claude/hooks/protect-files.sh`** — add or remove entries in `PROTECTED_PATTERNS` for your project's sensitive files (`appsettings.Production.json`, CI config, lock files, etc.). Matching is substring-based; exit code 2 blocks an edit.

6. **`.claude/agents/architecture-guardian.md`** — replace the layer boundaries with your architecture (hexagonal, clean architecture, microservice, …) — or just run `/bootstrap --architecture hexagonal`, which does this for you from a bundled template. State the *rules*; the agent discovers your actual classes by reading the source tree.

7. **`.claude/agents/spec-compliance.md`** — update the Processing Order / Numeric Precision / Feature Interactions / API Contract checks for your domain, or remove sections that don't apply.

8. **`docs/specs/`** — starts empty. Create one `<feature>.specs.md` per feature as you `/discover` them; every rule should end up with at least one acceptance test.

Keep as-is: the `.claude/` directory structure, the hook exit-code convention (exit 2 to block), the two-tier test layout, and the `Controllers/` / `Services/` / `Models/` folder names (or their `Domain/`/`Application/`/`Adapter/` hexagonal equivalents) — the skills and agents assume them.
