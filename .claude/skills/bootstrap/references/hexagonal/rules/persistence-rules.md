---
paths:
  - "src/**/Adapter/Out/Persistence/**"
---

You are editing a driven (outbound) persistence adapter.

- EF Core entities and `DbContext` usage live HERE, and only here — never in
  `Domain/` or `Application/`.
- Implement the `Application/Port/Out/` interface this adapter is for. The
  interface's method signatures use Domain types; map to/from the EF Core
  entity at this boundary.
- NEVER expose an EF Core entity outside this layer — a caller in
  `Application/` should only ever see Domain types.
- Prove this adapter against a REAL SQL engine. Every other tier substitutes a
  fake for this port, so this tier is the only place the real implementation is
  ever exercised; skipping it leaves an adapter that has never run against the
  thing it adapts. Prefer **Testcontainers** (the actual engine you deploy on)
  when Docker is available, **SQLite in-memory** when it isn't. The EF Core
  **InMemory provider is not a database** — no SQL, no constraints — so it
  never counts as this tier. See `.claude/skills/tdd/test-patterns.md` →
  "Repository / outbound adapter".
- **If `docs/context/adapter-testing-strategy.md` exists, it outranks the
  preference above** — that project has at least one dependency no container
  can stand in for, so which tier proves a given adapter is decided by the
  port → tier map in that doc, not by preferring containers.
- Writing this adapter's real implementation is an ordinary `/tdd` cycle at
  that tier — not a separate workflow, and not something to leave as a
  throwing stub once the port's callers are green.
- **Schema changes go through EF Core migrations, never `EnsureCreated()` and
  never a hand-edited database.** Add one with
  `dotnet ef migrations add <DescribesTheChange>`, commit the generated files,
  and let tests apply them with `await dbContext.Database.MigrateAsync()` so
  the schema under test is the one production gets. Never edit a migration
  that has already been applied anywhere — add a new one instead.

These boundaries are also audited by the `architecture-guardian` agent.
