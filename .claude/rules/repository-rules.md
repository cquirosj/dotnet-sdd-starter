---
paths:
  - "src/**/Repositories/**"
---

You are editing the repository layer — data access, and the only layer that
talks to the database.

- EF Core entities, `DbContext` usage, and any SQL live HERE, and only here —
  never in `Services/` or `Models/`. A caller in `Services/` should only ever
  see domain types; map to/from the persistence entity at this boundary.
- Constructor-inject the `DbContext`; no static or service-locator access.
- Return `Result<T>` (see `Shared/Result.cs`) for expected outcomes a caller
  can plan for (not-found, duplicate key). Reserve real exceptions for genuine
  infrastructure failure — an unreachable database, a broken connection.
- **Prove this repository against a REAL SQL engine.** Every other test tier
  substitutes a fake for this class, so this tier is the only place its real
  implementation is ever exercised; skip it and you ship a repository that has
  never run a query. Assert what only a real engine can prove: `decimal` scale
  surviving the round trip, constraints actually rejecting bad data, migrations
  applying cleanly.
- Prefer **Testcontainers** (the actual engine you deploy on, in a throwaway
  container) when Docker is available; fall back to **SQLite in-memory** when
  it isn't — still a real SQL engine, just a different dialect. The EF Core
  **InMemory provider is not a database**: it runs no SQL and ignores
  constraints, so it never counts as this tier. See
  `.claude/skills/tdd/test-patterns.md` → "Repository / outbound adapter" for
  the comparison table and a worked example.
- Writing this repository's real implementation is an ordinary `/tdd` cycle at
  that tier — not a separate workflow, and not something to defer once the
  services calling it are green.
- **Schema changes go through EF Core migrations, never `EnsureCreated()` and
  never a hand-edited database.** Add one with
  `dotnet ef migrations add <DescribesTheChange>`, commit the generated files,
  and let tests apply them with `await dbContext.Database.MigrateAsync()` so
  the schema under test is the same one production gets. `EnsureCreated()`
  bypasses migrations entirely and will quietly diverge from them.
- Never edit an applied migration — add a new one. Editing one that has run
  anywhere leaves other environments unable to reach the same schema.

These boundaries are also audited by the `architecture-guardian` agent.
