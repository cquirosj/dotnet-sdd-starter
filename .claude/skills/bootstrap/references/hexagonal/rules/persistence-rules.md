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
- Test with an EF Core in-memory provider or Testcontainers against a real
  engine — see `.claude/skills/tdd/test-patterns.md` → the repository tier.

These boundaries are also audited by the `architecture-guardian` agent.
