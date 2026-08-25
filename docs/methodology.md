# Methodology

This starter calls itself "SDD" (Spec-Driven Development) because that's the label under which the underlying technique is best known. But the name undersells what's actually happening, and it's worth being precise about it — especially because Claude reads this file too, and the distinction below is a behavioral instruction, not just background reading.

## Not just "spec-driven" — this is BDD, done properly, with real TDD discipline

Most things branded "spec-driven development" in the AI-tooling world today use a spec as a one-shot input to a code-generation step: write a description, generate an implementation from it, done. Nothing here works that way.

- **`/discover` is [Example Mapping](https://cucumber.io/blog/bdd/example-mapping-introduction/)** — Matt Wynne's technique for turning a user story into a structured conversation *before* anything gets written down as a test: one rule per card, examples that make each rule concrete, counter-examples that mark its deliberate boundaries, and open questions resolved on the spot. The output isn't prose — it's a shared, precise understanding of the behavior, saved to `docs/specs/<feature>.specs.md`.
- **What gets saved follows [Specification by Example](https://gojko.net/books/specification-by-example/)** — Gojko Adzic's discipline that a specification isn't real until every rule is illustrated with a concrete, executable example. A rule with no example is an opinion; a rule with an example is a test waiting to be written. That's exactly the shape `docs/specs/` files take, and it's why `/accept` can turn a spec rule directly into a failing test without re-interpreting anything.
- **`/accept` and `/tdd` hold genuine ATDD/TDD discipline** — the part that's easiest for an AI-assisted workflow to fake and easiest for a human to not notice was faked. `/accept` writes exactly one failing acceptance test for the next rule and stops. `/tdd` runs exactly one RED → GREEN → REFACTOR → CHALLENGE cycle and stops, waiting for a human before continuing. Neither skill is allowed to write production code before running the test and watching it fail *for the stated reason* — not "I expect this would fail," an actual failing test run, inspected.

That last point is the whole point. An LLM asked to "implement this spec" can always produce plausible-looking code on the first try — that's what it's good at. What it can't fake, if the workflow doesn't let it skip the step, is proof that the behavior didn't already exist before the change. Seeing red first is what turns "the AI wrote something that looks right" into "the AI proved this gap was real and then closed it." Everything else in this harness — the rules, the hooks, the agents — exists in service of keeping that one step honest.

## The four-step cycle

| Step | Skill | Does | Stops when |
|---|---|---|---|
| 1 | `/discover` | Example Mapping session on a user story: rules, examples, counter-examples, questions resolved interactively. Saves `docs/specs/<feature>.specs.md`. | The spec is presented for approval and saved — no code, no tests yet. |
| 2 | `/accept` | Writes ONE failing acceptance test for the next unimplemented rule, against the real HTTP endpoint. | The test exists and has been run — failing for the right reason. |
| 3 | `/tdd` | Runs ONE inner-loop cycle: RED (confirm the failure) → GREEN (minimum code to pass) → REFACTOR → CHALLENGE (propose the next edge case). | After one cycle — it does not chain into the next rule unsupervised. |
| 4 | `/review` | Read-only architecture + spec-compliance + test-quality audit of everything uncommitted (or the last commit, if the tree is clean). Produces a structured report (CRITICAL / WARNING / INFO) and a recommendation. Modifies nothing. | The report is presented — it's your call what to do with the findings. |

**When to run `/review`:** whenever you're about to commit, not on a fixed cadence. That can be after a single `/accept` + `/tdd` cycle if the rule was substantial, or once after a whole feature's rules are all green — the trigger is "I'm about to commit this," not "I just finished a cycle." It catches exactly what passing tests can't: architecture violations, weak assertions (`ShouldNotBeNull()` where a concrete value from the spec belongs), spec rules with no corresponding test, tests with no corresponding spec rule, and API contract drift if `docs/api/` has an OpenAPI spec for the feature. It never edits anything — it hands back a report and waits.

## The hooks' own evolution — a worked example of the same discipline

The two hooks this starter ships today (`protect-files.sh`, `session-start.sh`) aren't the only approach this methodology's author tried. Tracing the direct ancestor's git history shows a real experiment and a real reversal, which is worth knowing because it's the same "visibility over automation" principle playing out one level down:

1. **First attempt:** a `PostToolUse` hook running the full test suite after *every single edit*. Automatic, but noisy and slow — every `Edit` call paid the cost of a full test run, whether or not it was a meaningful checkpoint.
2. **Second attempt:** a `Stop`-event hook (`stop-gate.sh`) that ran the build and refused to let the session end if it was red — a hard, automatic gate on stopping with broken code.
3. **Settled design:** both were removed, in favor of what exists today — `/accept` and `/tdd` run tests explicitly, as a visible step inside each cycle, narrated in the transcript rather than enforced silently by a hook that only fires when Claude tries to stop talking.

The reasoning generalizes: a hook that *enforces* red-green automatically is invisible — you get the safety property, but you (or a reviewer reading the transcript later) can't see it happen. A skill that *narrates* red-green as an explicit step gives up nothing in rigor and gains a transcript a human can actually audit. That's the same tradeoff behind everything in this file: rigor that's visible beats rigor that's merely automatic.

`protect-files.sh` (PreToolUse) and `session-start.sh` (SessionStart) survived that reduction because neither one is about red/green at all — one's a hard guardrail against touching secrets/prod-config/CI, the other is pure orientation (branch, uncommitted/staged diff, a workflow reminder) with no gating behavior. Nothing about either conflicts with keeping the TDD cycle itself visible.

## Quality agents and mutation testing

Distinct from the four-step cycle above, four agents audit different concerns on demand — not run in the auto-cycle, invoked when you want that particular lens:

| Agent | Checks | Typical trigger |
|---|---|---|
| `architecture-guardian` | Layer boundaries: controllers stay thin, services hold logic, models stay data-only, dependencies flow one direction. | After writing/changing production code, before committing. |
| `spec-compliance` | Every spec rule has a test, monetary precision holds, feature interactions and the API contract are covered. | Before committing a feature implementation. |
| `mutation-analyst` | Runs Stryker.NET, reads the mutation report, and explains each **surviving** mutant — what changed, why no test caught it, and a specific assertion to kill it. Prioritizes financial/business logic over utility code. Never fixes tests itself. | After the test suite is green and stable for a feature — mutation testing measures assertion *strength*, not correctness, so it's only meaningful once the happy-path tests already pass. |
| `config-auditor` | Audits the `.claude/` setup itself — token efficiency, whether triggers actually fire, safety of hook/permission configuration. | When the setup feels slow, expensive, or unreliable — not part of feature work. |

**Running mutation testing:** Stryker.NET doesn't run itself. From the repo root (or a test project directory), run:

```bash
dotnet stryker
```

This produces a report under `StrykerOutput/<timestamp>/reports/` (JSON + an HTML view). Once that exists, either invoke `mutation-analyst` directly for the surviving-mutant analysis above, or run the consolidated pipeline:

```
/quality-check
```

which chains `spec-compliance` → `architecture-guardian` → `mutation-analyst` (skipped with a note if no `StrykerOutput/` exists yet) into one report written to `quality-report.md`. That file is gitignored deliberately — it's a point-in-time snapshot, regenerate it, don't commit it.

## Full inventory

**Skills** (`.claude/skills/`, invoked by name as a slash command):

| Skill | Model | Role |
|---|---|---|
| `bootstrap` | opus | Adapts this starter to a real project — interactive on a fresh clone, zero-prompt auto-detection when retrofitted onto an existing codebase. `--architecture hexagonal` for ports & adapters. |
| `discover` | opus | Step 1 — Example Mapping. |
| `accept` | sonnet | Step 2 — one failing acceptance test. |
| `tdd` | sonnet | Step 3 — one RED→GREEN→REFACTOR→CHALLENGE cycle. |
| `review` | opus | Step 4 — read-only architecture/spec/test-quality report. |
| `commit-summary` | (forked context) | Summarizes the current branch's changes and suggests a commit message. |
| `claudius` | — | Audits this `.claude/` configuration itself — token efficiency, trigger reliability, hook/permission safety. Use when the setup feels wrong, not as part of feature work. |

**Commands** (`.claude/commands/`):

| Command | Role |
|---|---|
| `quality-check` | Chains `spec-compliance` → `architecture-guardian` → `mutation-analyst` into `quality-report.md`. |

**Agents** (`.claude/agents/`): `architecture-guardian`, `spec-compliance`, `mutation-analyst`, `config-auditor` — see the table above.

**Hooks** (`.claude/hooks/`, wired in `.claude/settings.json`): `protect-files.sh` (PreToolUse — blocks edits to secrets/prod-config/CI), `session-start.sh` (SessionStart — orientation, no gating).

## Origins and further reading

This starter is a from-scratch ASP.NET Core / .NET port of [`serenity-dojo/claude-springboot-starter`](https://github.com/serenity-dojo/claude-springboot-starter) — same `.claude/` harness, same methodology, Java original. The Java template's own history is where the hook experiment described above actually happened; this port carries the settled design forward rather than re-running the experiment.

The fuller, section-by-section walkthrough of *why* each piece of this harness exists — rules, hooks, skills, subagents, in the order they'd naturally get introduced to someone learning the approach — lives in [`serenity-dojo/cashback-rewards`](https://github.com/serenity-dojo/cashback-rewards), the hands-on project for the Udemy course *Spec-Driven Development and TDD with AI*. Its README's "Branch Map" section is worth reading directly: each course section has a `start`/`solution` branch pair and a one-line note on what that section introduces. This starter's hexagonal-architecture templates (`.claude/skills/bootstrap/references/hexagonal/`) were extracted from that repo's `section-13/solution` branch, its most complete state.

For the underlying practices by name, independent of either repo:

- Gojko Adzic, *[Specification by Example](https://gojko.net/books/specification-by-example/)* — why a spec isn't real until it's illustrated with concrete examples.
- Matt Wynne, *[Example Mapping](https://cucumber.io/blog/bdd/example-mapping-introduction/)* — the rules/examples/questions technique `/discover` implements.
