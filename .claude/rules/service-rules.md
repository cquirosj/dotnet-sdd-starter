---
paths:
  - "src/**/Services/**"
---

You are editing the service layer — all business logic and orchestration.

- This layer must be testable with plain xUnit, no hosted ASP.NET Core context.
  Keep HTTP and persistence concerns out: never import `Microsoft.AspNetCore.Mvc`
  or controller-layer types.
- Return `Result<T>` (see `Shared/Result.cs`) for anything that can fail for an
  *expected* domain reason (validation, not-found, conflict) — never throw for
  these. Reserve real exceptions for truly exceptional/infrastructure conditions
  a caller can't reasonably plan for.
- Money and exact decimals follow the invariant in CLAUDE.md (`decimal`, never
  `double`/`float`). Round with `Math.Round(value, 2, MidpointRounding.AwayFromZero)`
  unless CLAUDE.md's Monetary/Numeric Precision section says otherwise.
- When a feature applies a fixed sequence of steps, follow the Processing Order
  in CLAUDE.md exactly — code and tests must not reorder it.
- If a rule keeps accreting cases inside a private method, that is design
  pressure: extract it into its own value object or service with its own seam,
  and test it directly at the lower tier.

These boundaries are also audited by the `architecture-guardian` agent.
