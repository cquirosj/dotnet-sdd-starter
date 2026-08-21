---
paths:
  - "src/**/Models/**"
---

You are editing the model layer — domain objects, request/response DTOs, enums,
and value objects.

- Hold data, not logic. No business rules, calculation, or orchestration — those
  belong in the service layer.
- Immutable where practical; prefer `record` types for value objects and DTOs.
- Monetary fields are `decimal`. No framework coupling (no `Microsoft.AspNetCore.*`
  or persistence attributes) in domain value objects.
