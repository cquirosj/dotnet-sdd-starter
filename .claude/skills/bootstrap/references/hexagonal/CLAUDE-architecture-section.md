## Architecture

Hexagonal (ports & adapters). Dependencies flow inward — Adapter → Application → Domain — and never the other way:

- **Domain/Model/**, **Domain/Service/** — the innermost hexagon. Pure C#: no ASP.NET Core, no EF Core, no framework dependency of any kind. `Model/` holds entities and value objects; `Service/` holds business logic that needs no outbound port.
- **Application/Port/In/** — interfaces describing use cases (what the outside world can ask the application to do).
- **Application/Port/Out/** — interfaces describing what the application needs from the outside world (a repository, a clock, an external client). Implemented by an adapter, defined here.
- **Application/Service/** — implements the inbound ports; orchestrates outbound ports and domain services. Orchestration only, no business logic that belongs in `Domain/Service/`.
- **Adapter/In/Web/** — ASP.NET Core controllers. Depend on `Application/Port/In/` interfaces, never a concrete service. Thin: no business logic, DTOs only over HTTP.
- **Adapter/Out/Persistence/** — implements `Application/Port/Out/` interfaces. EF Core entities and `DbContext` usage live here, never in Domain or Application.

Layer rules: Domain imports nothing outward. Application imports Domain and its own port interfaces, never a concrete Adapter class. Adapters depend inward only. The `architecture-guardian` agent enforces these boundaries.

<!-- ADAPT: If you use a different style (simple layered: controller/service/
     model, clean architecture: entities/usecases/interfaces, microservices),
     replace the layers and rules above to describe it, and update
     .claude/agents/architecture-guardian.md to match. -->
