# ASP.NET Core SDD Starter

A starter for building ASP.NET Core services using a **Spec-Driven Development (SDD)** workflow with Claude Code. It bundles a working ASP.NET Core / .NET 10 solution together with a `.claude/` toolchain — skills, agents, and hooks — that turns business rules into specs, specs into acceptance tests, and tests into code.

"SDD" undersells it, though. `/discover` is **Example Mapping**; the specs it produces follow **Specification by Example**; and `/accept` + `/tdd` hold real ATDD/TDD discipline — production code is never written before its test has run and failed for the stated reason. See [`docs/methodology.md`](docs/methodology.md) for the full case, the four-step cycle in detail, and where this comes from.

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

## The Development Workflow

Claude Code drives a feature through four steps, each backed by a skill you invoke by name:

1. **`/discover`** — Turn a feature idea into a spec using Example Mapping (rule → example → counter-example → edge cases → open questions). Saves a draft spec to `docs/specs/<feature>.specs.md`.
2. **`/accept`** — Write ONE failing acceptance test for the next rule in a spec, hitting the real endpoint.
3. **`/tdd`** — Run one TDD inner-loop cycle (RED → GREEN → REFACTOR → CHALLENGE) to drive that test to green with the minimum code, then stop and propose the next edge case.
4. **`/review`** — Read-only architecture, spec-compliance, and test-quality report of everything uncommitted. Run it whenever you're about to commit — after one cycle or after a whole feature, whichever makes sense — to catch what passing tests can't reveal (architecture violations, weak assertions, missing spec coverage, contract drift). It modifies nothing; it hands back a report and waits.

Full detail on each step, and why the RED step in particular is non-negotiable, is in [`docs/methodology.md`](docs/methodology.md).

Four agents enforce quality **on demand**, separately from the four-step cycle above:

- **`architecture-guardian`** — checks that code respects the layer boundaries (controllers stay thin, services hold the logic, models stay data-only).
- **`spec-compliance`** — checks that every spec rule has a test, that precision/ordering rules hold, and that the API contract matches.
- **`mutation-analyst`** — runs Stryker.NET (`dotnet stryker`) and reports which **surviving** mutants indicate weak assertions, with a suggested fix for each.
- **`config-auditor`** — audits the `.claude/` setup itself for token efficiency and trigger reliability.

Two hooks (configured in `.claude/settings.json`) run automatically:

- **PreToolUse** — `protect-files.sh` blocks edits to protected files (prod config, secrets, CI). Tests are run explicitly by the `/accept` and `/tdd` skills as part of each cycle, not by a hook, so the red/green steps stay visible turn by turn.
- **SessionStart** — `session-start.sh` prints the current branch and any uncommitted/staged changes at the top of every new or resumed session, plus a one-line workflow reminder (`/discover > /accept > /tdd`) — pure orientation, no gating.

The `/quality-check` command chains spec-compliance → architecture-guardian → mutation-analyst (run `dotnet stryker` first if you want that last one included) into one consolidated report at `quality-report.md` (gitignored — regenerate, don't commit).

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
.claude/hooks/                                   protect-files.sh (file guard), session-start.sh (branch/diff orientation)
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

## Origins & Further Reading

This is a from-scratch ASP.NET Core / .NET port of [`serenity-dojo/claude-springboot-starter`](https://github.com/serenity-dojo/claude-springboot-starter) — same `.claude/` harness and methodology, Java original. The hexagonal-architecture templates (`.claude/skills/bootstrap/references/hexagonal/`) were extracted from [`serenity-dojo/cashback-rewards`](https://github.com/serenity-dojo/cashback-rewards)'s `section-13/solution` branch, the hands-on project for the Udemy course *Spec-Driven Development and TDD with AI* — its README's "Branch Map" section is a good section-by-section tour of why each piece of this kind of harness exists.

See [`docs/methodology.md`](docs/methodology.md) for the full picture: why this is closer to BDD (Specification by Example, Example Mapping) with genuine ATDD/TDD discipline than a typical "spec-driven" code-generation flow, the four-step cycle in detail, the hooks' own history, and the quality/mutation-testing tooling.
