---
paths:
  - "src/**/Domain/**"
---

You are editing Domain code — the innermost hexagon.

- Pure C#. NEVER `using Microsoft.AspNetCore.*`. NEVER any EF Core / persistence
  `using`. Dependencies flow inward; Domain depends on nothing outward — not
  Application, not Adapter.
- `Domain/Model/` — entities and value objects. Prefer `record` types.
- `Domain/Service/` — business logic that needs no port (no repository, no
  external call). If the logic needs a repository or other outbound
  dependency, it belongs in `Application/Service/` instead, not here.
- Expected business-rule failures return `Result<TValue, TError>` (see
  CLAUDE.md → Error Handling) — never thrown. This holds even where other
  hexagonal codebases would reach for a domain exception; this starter's
  functional-error convention applies uniformly across every layer.
- Money handling: see the money/precision rule in CLAUDE.md — `decimal` only.
- Test with plain xUnit + Shouldly, no hosted ASP.NET Core context.

These boundaries are also audited by the `architecture-guardian` agent.
