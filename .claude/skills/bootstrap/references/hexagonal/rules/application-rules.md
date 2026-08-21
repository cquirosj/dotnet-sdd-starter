---
paths:
  - "src/**/Application/**"
---

You are editing Application code — inbound/outbound ports and the services
that orchestrate them.

- `Application/Port/In/` — interfaces describing use cases. One per use case;
  a single method is fine. Adapters (controllers) depend on these interfaces,
  never on the concrete service class.
- `Application/Port/Out/` — interfaces describing what the application needs
  from the outside world (repository, clock, external client). Implemented in
  `Adapter/Out/`, never here.
- `Application/Service/` — implements the inbound ports. Orchestration only:
  call outbound ports, call Domain services, assemble the result. No business
  logic that belongs in `Domain/Service/`, no `Microsoft.AspNetCore.*` types,
  no EF Core types — depend on the port interfaces, not on a concrete adapter.
- Return `Result<TValue, TError>` from every operation that can fail for an
  expected reason (validation, not-found, conflict) — never throw. Reserve
  real exceptions for truly exceptional/infrastructure failures a caller can't
  reasonably plan for.
- Test with plain xUnit, constructing the service directly with a
  hand-written or Moq'd `Application/Port/Out/` implementation — no hosted
  ASP.NET Core context needed here either.

These boundaries are also audited by the `architecture-guardian` agent.
