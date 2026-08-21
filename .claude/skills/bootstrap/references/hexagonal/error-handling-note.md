# Why hexagonal mode still uses Result<T>, not exceptions

This starter's hexagonal architecture-guardian, rules, and CLAUDE.md section
were extracted from a real hexagonal ASP.NET/Spring reference implementation
(a course solution built around ports & adapters). That reference project uses
**thrown domain exceptions**, mapped to HTTP status codes centrally: an
application service throws (e.g. `MerchantAlreadyRegisteredException`), and a
global exception handler translates it to the right response.

This starter's bootstrap does **not** carry that pattern over. Every mode this
skill produces — layered or hexagonal — uses the same functional
`Result<TValue, TError>` convention documented in CLAUDE.md → Error Handling:

- Domain and Application methods that can fail for an *expected* business
  reason return `Result<TValue, TError>.Failure(...)`. They never throw for
  this.
- The web adapter (controller) maps the `Result` to an HTTP status via
  `.Match(...)` — there is no exception-handler middleware for domain errors,
  in hexagonal mode any more than in layered mode.
- Real exceptions are still reserved for truly exceptional/infrastructure
  conditions (a failed database connection, a bug) — same rule as layered
  mode, same rule as before this starter existed.

When translating the reference implementation's patterns into this starter's
hexagonal templates (architecture-guardian.md, the rules files, the CLAUDE.md
Architecture section), the **folder structure and dependency-direction rules**
were kept faithfully — those are genuinely how hexagonal architecture works.
The **error-handling mechanism** was deliberately swapped for `Result<T>`
everywhere the reference used `throw`. If you ever reference the original
public course-solution repo for inspiration on this codebase, keep that one
substitution in mind — don't reintroduce exceptions-for-control-flow while
porting an idea from it.
