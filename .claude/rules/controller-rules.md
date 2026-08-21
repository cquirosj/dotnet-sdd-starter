---
paths:
  - "src/**/Controllers/**"
---

You are editing a REST controller — the web layer.

- Keep controllers thin: receive the request, delegate to a service, return the
  response. No business logic, no calculation, no branching on domain state.
- Accept and return JSON DTOs (request/response models). Map a service's `Result<T>`
  failure to an HTTP status code here via `.Match(...)` — this is the only layer
  that knows about HTTP. Be explicit: 200 success, 400 validation, 401/403 auth,
  404 not found, 409 conflict.
- Use constructor injection. Never reach into persistence or framework internals
  from here, and never wrap a service call in try/catch for an expected domain
  failure — that's what `Result<T>` is for.

These boundaries are also audited by the `architecture-guardian` agent.
