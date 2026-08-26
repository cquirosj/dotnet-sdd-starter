# ADAPT checklist

One entry per ADAPT point. For each: **Ask** is the greenfield interview
question (offer these as `AskUserQuestion` options plus room for a free-text
answer via "Other"); **Infer** is what to read/grep in brownfield mode instead.
Resolve every point — don't skip one because it feels obvious; a skipped point
is exactly how a stale placeholder survives.

## 1. Project name / namespace

- **Ask:** "What's this project called?" (used for the `.sln`/`.slnx` name,
  root namespace, and assembly names).
- **Infer:** the target repo's own folder name, or an existing `.sln`'s name if
  one already exists (brownfield only touches `.claude/` and `CLAUDE.md`, so
  there's usually already a real name to read, not invent).

## 2. Domain description

- **Ask:** "One or two sentences: what does this service actually do?"
- **Infer:** read the repo's own `README.md` if present, the names of existing
  controllers/use-cases, and any existing `docs/` — synthesize a description
  from what the code already does. Flag as low-confidence if the codebase is
  too sparse to tell.

## 3. Architecture

Resolved in Step 0 of the main skill, not here — this entry exists only so the
checklist stays a complete map of CLAUDE.md's sections.

## 4. Processing order

- **Ask:** "Does any feature apply a fixed sequence of steps (e.g. price →
  discount → tax → total)? If so, what's the order?" If no, delete the
  section.
- **Infer:** grep existing service/domain code for multi-step calculation
  methods with named intermediate variables — a strong signal of an
  order-dependent chain worth documenting. If nothing looks order-dependent,
  delete the section rather than leaving it empty.

## 5. Monetary / numeric precision

- **Ask:** "Does this project handle money or other values that need exact
  decimal precision? If so, what rounding rule (HALF_UP / HALF_EVEN / other)
  and how many decimal places?" If no, delete the section.
- **Infer:** grep for `decimal` usage in the existing codebase. If present,
  look for existing `Math.Round(...)` calls to infer the rounding mode already
  in use rather than assuming `MidpointRounding.AwayFromZero`; if `decimal` is
  used but no rounding logic exists yet, default to `AwayFromZero` (HALF_UP
  equivalent) and flag as an assumption. If no `decimal` usage exists anywhere,
  delete the section.

## 6. Error handling

Not a question — this starter's functional `Result<TValue, TError>` /
`Error` convention (see `Common/Result.cs` in layered mode, or the equivalent
path in hexagonal mode) is a fixed default, not something this skill offers to
turn off. **Brownfield exception:** if the existing project already has an
established, different error-handling convention in wide use (a Problem
Details middleware, an existing exception hierarchy with its own HTTP mapping,
etc.), do not silently introduce a second, competing pattern — note the
conflict explicitly in the final report as something the user must resolve by
hand, and leave the existing code's pattern alone rather than overwriting it.

## 7. API design

- **Ask:** "What's the first real endpoint going to look like? (Method, path,
  request/response shape.) It's fine to leave this generic if you haven't
  designed it yet — you'll fill it in with `/discover` anyway."
- **Infer:** read one existing controller/endpoint if any exist, and use it as
  the concrete example instead of the generic placeholder. If none exist yet,
  leave the generic `POST /api/<resource>` placeholder from the template as-is
  — don't invent a fictional endpoint.

## 8. Security

- **Ask:** "Does this service need authentication? If so, what mechanism (API
  key header, JWT, OAuth2)?" If no, delete the section (matches the starter's
  own default: unauthenticated until configured).
- **Infer:** grep for `AddAuthentication`, `[Authorize]`, or an existing auth
  middleware registration in `Program.cs`. If found, describe the mechanism
  already in place rather than proposing a new one. If not found, delete the
  section.

## 9. Protected files (`protect-files.sh`)

- **Ask:** "Any project-specific files Claude should never edit, beyond the
  defaults (prod config, secrets, CI)?"
- **Infer:** look for real files matching common sensitive patterns beyond the
  defaults already in `protect-files.sh` — `docker-compose*.yml`,
  `Dockerfile`, `terraform/`, `k8s/`, any `appsettings.*.json` variant beyond
  `Development`/`Production`. Add patterns for what actually exists; don't add
  patterns for files that aren't there.

## 10. Test command

- **Ask/Infer (both modes):** confirm `dotnet test` is the right command (it
  almost always is for a `.sln`-based project). If the project uses a custom
  build wrapper or a non-standard test invocation, update
  `.claude/settings.json`'s hook commands and the `/accept`/`/tdd` skills'
  references to match.

## 11. Outbound dependencies (database, broker, external API)

Decides whether the repository / outbound-adapter test tier applies. The
worked example ships with none, so the starter's default is "not yet."

- **Ask (greenfield):** "Will this service talk to a database, message broker,
  cache, or third-party API?" Offer: a relational database (EF Core) · a
  broker or cache · a third-party HTTP API · none yet.
- **Infer (brownfield):** grep the `.csproj` files for
  `Microsoft.EntityFrameworkCore*`, `Npgsql*`, `Microsoft.Data.SqlClient`,
  `StackExchange.Redis`, `RabbitMQ.Client`, `Azure.Messaging.*`, `MassTransit`,
  or a registered `HttpClient`/`IHttpClientFactory`; and look for a
  `Repositories/`, `Adapter/Out/`, or `*DbContext.cs`.

**If there is at least one:**

- Keep CLAUDE.md's *Proving real implementations* section and the repository
  tier row in its Testing Conventions, naming the actual dependencies.
- Keep `.claude/rules/repository-rules.md` (layered) or
  `.claude/rules/persistence-rules.md` (hexagonal), and correct its `paths:`
  frontmatter to the folder this project actually uses.
- Add the test package for the engine you'll prove against: the matching
  Testcontainers one (`Testcontainers.PostgreSql`, `Testcontainers.MsSql`,
  `Testcontainers.RabbitMq`, … — one per engine, not the meta-package) when
  Docker is available, or `Microsoft.Data.Sqlite` for the no-Docker fallback.
  Ask (greenfield) / check for a `Dockerfile`, compose file, or CI service
  container (brownfield) before assuming Docker is on hand.
- **Relational database specifically:** confirm schema changes go through EF
  Core migrations. **Brownfield:** look for a `Migrations/` folder next to the
  `DbContext`; if there is none but there is a `DbContext`, or if you find
  `EnsureCreated()` in startup or test setup, flag it — that project builds
  its schema from the model and has no migration history, so the repository
  tier can pass against a schema production would never get.
- **Brownfield:** if repositories/adapters already exist with no test that
  ever runs them against a real engine, say so plainly in the final report.
  That's the gap this tier exists to close, and it's the single most valuable
  thing this bootstrap can surface — but adding those tests is later `/tdd`
  work, not something this skill does inline.

**If there are none:** delete CLAUDE.md's *Proving real implementations*
section (per its own ADAPT comment), drop the repository tier row from Testing
Conventions, and delete the repository/persistence rules file. Don't leave a
section describing a tier this project has nothing to put in — and say in the
final report that it was removed, so it's re-added deliberately when the first
real dependency arrives.
