---
paths:
  - "src/**/Adapter/In/Web/**"
---

You are editing a driving (inbound) web adapter — a REST controller.

- Thin: receive the request, call an `Application/Port/In/` use-case interface
  immediately, map its `Result<T>` to an HTTP response. No business logic.
- Depend on the `Application/Port/In/` interface, never the concrete
  `Application/Service/` class — that's the whole point of the port.
- Use DTOs (records), not Domain types, over HTTP. Map DTO ↔ Domain type at
  this boundary.
- Constructor injection only.
- Data-annotate request DTOs for model-binding validation.
- Map a `Result<T>` failure to an HTTP status here via `.Match(...)` — this is
  the only layer that knows about HTTP. Be explicit: 200/201 success, 400
  validation, 401/403 auth, 404 not found, 409 conflict. Never swallow a
  failure or leak infrastructure details (a raw exception message, a stack
  trace) into the response body.
- Test with a `WebApplicationFactory` slice that swaps the real
  `Application/Port/In/` implementation for a Moq mock, to prove routing and
  serialization without exercising real business logic (see
  `.claude/skills/tdd/test-patterns.md` → tier 3).

These boundaries are also audited by the `architecture-guardian` agent.
