---
name: bootstrap
model: claude-opus-5
allowed-tools: Read, Write, Edit, Bash, Glob, Grep, AskUserQuestion, WebFetch
description: >-
  Adapt this SDD starter to a real project by resolving every ADAPT point in
  CLAUDE.md and the .claude/ harness — or retrofit the same .claude/ toolchain
  onto an EXISTING ASP.NET Core project that doesn't have it yet. Interviews
  you when run on a fresh clone of this starter (greenfield); auto-detects
  everything by reading the existing codebase with zero prompts when run
  inside an existing project (brownfield). Pass --architecture hexagonal to
  bootstrap with ports-and-adapters instead of the default layered style.
  Use once, immediately after cloning this starter, or immediately after
  deciding an existing project should adopt this SDD workflow.
argument-hint: "[--mode greenfield|brownfield] [--architecture layered|hexagonal]"
---

Resolve every ADAPT point for this project: $ARGUMENTS

This skill automates the "Adapting This Starter" section of this repo's own
README.md. Read that section now if you haven't already this session — it's
the checklist this skill exists to walk, either by asking (greenfield) or by
reading the existing codebase (brownfield).

## Step 0 — Parse arguments, then determine mode

Parse `--mode` and `--architecture` out of `$ARGUMENTS` if present. For
whichever one is missing, determine it as follows — **do not ask the user for
`--mode`**; detect it, because the detection itself is what tells you whether
asking is even allowed for the rest of this skill.

**Mode detection:**
1. Read `CLAUDE.md` in the current working tree. If it contains the literal
   marker `<!-- ADAPT` **and** the source tree still contains the
   `ShippingCalculator.*` namespaces from this starter's worked example →
   **greenfield**. You're running inside a fresh (or partially-adapted) clone
   of this starter itself.
2. Otherwise, if `.claude/skills/discover/SKILL.md` does **not** exist in the
   current repo at all → **brownfield**. You're being asked to retrofit this
   toolchain onto a project that doesn't have it.
3. Otherwise (a `.claude/` harness exists, but it isn't this starter's
   pristine worked example) → treat as **greenfield**, idempotently: resolve
   whatever ADAPT markers remain, and skip any step whose target already looks
   adapted (e.g. don't ask for a project name that's already been renamed).

**Architecture detection** (only if `--architecture` wasn't passed):
- **Greenfield:** ask with `AskUserQuestion` — "Layered (Controllers → Services
  → Models, simplest)" vs "Hexagonal (Ports & Adapters — domain/application/
  adapter, best when you want the domain fully framework-free and testable in
  isolation, or expect multiple driving/driven adapters over time)".
- **Brownfield:** infer from the existing source tree. Look for
  `Ports/`/`Adapters/`/`UseCase` naming or a `Domain`/`Application`/
  `Infrastructure` project split → hexagonal. Look for `Controllers/`/
  `Services/`/`Models/` → layered. If genuinely ambiguous (near-empty project),
  default to **layered** and say so plainly in the final report — don't guess
  silently.

State the detected mode and architecture back to the user in one line before
proceeding (e.g. "Detected: brownfield, hexagonal (found `Ports/` and
`Adapters/`)."), so a wrong detection can be caught before 20 files change. In
greenfield mode this is a statement, not a question — keep moving. In
brownfield mode, since you won't be asking anything else either, this one line
is the ONLY chance the user gets to sanity-check your read of their repo, so
make it specific enough to be worth catching (name what you saw, not just the
conclusion).

## Step 1 (brownfield only) — Fetch and install the harness

Skip this step entirely in greenfield mode — the harness is already on disk.

The target repo has no `.claude/discover` etc., so bring the toolchain in from
the public starter repo: **https://github.com/cquirosj/dotnet-sdd-starter**
(branch `main`).

1. **Verify the target stack first.** This starter assumes ASP.NET Core / .NET.
   Check for a `.sln`, `.csproj`, or `global.json`. If none exist, STOP and
   tell the user this skill only knows how to bootstrap ASP.NET Core / .NET
   projects — don't force a mismatched harness onto a different stack.
2. Shallow-clone the starter into a scratch directory (don't pollute the
   target repo's own git state):
   ```bash
   git clone --depth 1 https://github.com/cquirosj/dotnet-sdd-starter /tmp/dotnet-sdd-starter-src
   ```
   If `git clone` isn't viable (no network, blocked), fall back to fetching
   individual files with `WebFetch` against
   `https://raw.githubusercontent.com/cquirosj/dotnet-sdd-starter/main/<path>`.
3. Copy into the target repo's `.claude/`:
   - `.claude/skills/discover/`, `accept/`, `tdd/`, `review/`, `commit-summary/`,
     `claudius/` — copied as-is. These are architecture- and stack-agnostic
     methodology; nothing here needs the source repo's own domain facts.
   - `.claude/commands/quality-check.md` — as-is.
   - `.claude/hooks/protect-files.sh` — as-is, then check Step 3h below for
     project-specific additions.
   - `.claude/agents/config-auditor.md`, `.claude/agents/mutation-analyst.md` —
     as-is.
   - `.claude/agents/architecture-guardian.md` and `.claude/rules/*.md` — **use
     the layered originals if `--architecture layered`, or the bundled
     hexagonal set in `references/hexagonal/` (relative to this skill) if
     `--architecture hexagonal`.** The hexagonal set lives inside THIS skill
     (not the fetched clone) because it was authored specifically for this
     bootstrap flow — see Step 3's architecture note for what's in it.
   - `.claude/hooks/session-start.sh` — already present in the fetched clone
     for both architectures now; nothing to copy.
   - `.claude/settings.json` — architecture-conditional: the fetched clone's
     `settings.json` for layered (already includes the `SessionStart` hook),
     or `references/hexagonal/settings.json` for hexagonal, which overwrites
     it with a stricter protected-file list and a `PreToolUse` matcher that
     also covers `Bash` — see that file for why.
   - `CLAUDE.md` — do NOT copy the fetched clone's CLAUDE.md verbatim (it
     describes the shipping-calculator worked example, which is irrelevant
     here). Instead, write a fresh CLAUDE.md using the ADAPT section structure
     from the fetched clone as your skeleton — Project Overview, Architecture,
     Processing Order, Monetary/Numeric Precision, Error Handling, API Design,
     Testing Conventions, Spec Files, API Documentation, Security, The
     `.claude/` Toolchain — filled with what you infer about the EXISTING
     project in Step 3, with NO Worked Example section (there's nothing to
     showcase; the real code already exists).
4. Create `docs/specs/` (empty) and `docs/api/` (empty) if they don't exist.

## Step 2 — Read the ADAPT checklist

Read **`references/adapt-checklist.md`** (relative to this skill) now. It's
the same list of ADAPT points whether you're interviewing (greenfield) or
inferring (brownfield) — that file states, for each point, both the question
to ask and what to grep/read for to infer it instead.

## Step 3 — Resolve every ADAPT point

For each item in the checklist:

- **Greenfield:** ask with `AskUserQuestion`, one item at a time (or batched
  where the tool allows, max 4 per call) — offer sensible defaults as options,
  never a bare open text box when a short list of real choices exists.
- **Brownfield:** infer by reading the existing code (`Read`, `Glob`, `Grep`).
  **Do not call `AskUserQuestion` at all in brownfield mode** — the point of
  this mode is zero interaction. If a point genuinely can't be inferred with
  reasonable confidence, pick the starter's own default, and record it as a
  **low-confidence assumption** in the final report (Step 6) rather than
  guessing silently or blocking.

Apply each resolved answer immediately: edit `CLAUDE.md` section by section,
removing its `<!-- ADAPT -->` comment once that section is accurate. Sections
with no relevance to this project (no processing order, no exact-decimal
values, no REST API) get deleted entirely, per their own ADAPT comment's
instructions — don't leave a section that says "N/A" or "not applicable."

**Architecture note (both modes, greenfield only actually _swaps_ files):**
If hexagonal was chosen and you're in greenfield mode (the harness already
exists on disk as the layered default), replace these files with the bundled
hexagonal versions from `references/hexagonal/` (relative to this skill):
`.claude/agents/architecture-guardian.md`, `.claude/rules/domain-rules.md`,
`.claude/rules/application-rules.md`, `.claude/rules/web-rules.md`,
`.claude/rules/persistence-rules.md` (delete the layered `controller-rules.md`,
`service-rules.md`, and `repository-rules.md` — they don't apply; the hexagonal
`persistence-rules.md` carries the same repository/Testcontainers guidance for
`Adapter/Out/Persistence/`), `.claude/settings.json` (for its
stricter protected-file list and `Bash`-covering `PreToolUse` matcher —
`.claude/hooks/session-start.sh` itself needs no change, it's already
architecture-agnostic), and CLAUDE.md's Architecture section (use
`references/hexagonal/CLAUDE-architecture-section.md` as the replacement
section body). Also **restructure the worked example's own folders** to match:
`src/<Project>.Domain/Models/` → split into `Domain/Model/` (pure records/
enums) and `Domain/Service/` (the calculation logic, if any belongs at the
domain tier rather than orchestration); introduce `Application/Port/In/`
(use-case interfaces), `Application/Port/Out/` (repository/output port
interfaces, even if nothing implements one yet), `Application/Service/`
(orchestration only); rename `src/<Project>.Api/Controllers/` to
`Adapter/In/Web/` conceptually (ASP.NET Core still discovers controllers by
base class, not folder name, so the folder rename is a convention, not a
framework requirement — say so in a comment if you do it). This is real file
surgery, not just a documentation edit — run `dotnet build` after and fix
anything the move broke before continuing.

**Read `references/hexagonal/error-handling-note.md` before touching any
hexagonal file** — the reference hexagonal repo this template's hexagonal mode
was extracted from (a course solution, not a template) uses thrown domain
exceptions mapped to HTTP centrally. This starter deliberately does NOT carry
that over: hexagonal mode here still uses `Result<TValue, TError>` end to end,
exactly like layered mode. The only things hexagonal mode changes are the
folder/namespace boundaries and the port/adapter vocabulary — never
reintroduce exceptions-for-control-flow while adapting these files.

## Step 4 — Rename (greenfield only)

Brownfield mode never renames existing application code — it only adds the
`.claude/` harness and a fresh `CLAUDE.md`. Skip this step in brownfield mode.

In greenfield mode, once you know the real project name:
1. Rename the `.sln`/`.slnx` file, every `.csproj`'s containing folder, and the
   root namespace (search-and-replace `ShippingCalculator` → the new name
   across every `.cs`, `.csproj`, and `.sln`/`.slnx` file — use `Grep` first to
   find every occurrence, don't assume you found them all by memory).
2. Ask (one question) whether to keep the shipping-calculator worked example
   as running reference code, or delete it now. Default recommendation: keep
   it for one more cycle so there's a working example to compare against while
   getting used to the workflow, and delete it once the first real feature
   lands. Either way, the CLAUDE.md Worked Example section is deleted in Step
   3 regardless — that's documentation, independent of whether the code stays.
3. Run `dotnet build && dotnet test` and fix anything the rename broke before
   continuing. A rename that leaves the solution non-building is not done.

## Step 5 — Verify

Run `dotnet build && dotnet test` (again, if not just run in Step 4) from the
repo root. Every test must still pass — the harness changes and any renaming
must never break existing green tests. If something fails, fix it; don't hand
back a broken build with an explanation.

## Step 6 — Report

Summarize what you did:

- Mode and architecture (detected or given), with the reasoning if detected.
- Every ADAPT point and its resolution (one line each).
- **Brownfield only:** call out every low-confidence assumption explicitly, in
  its own list, so the user knows exactly what to double check — this mode
  never asked, so this is the only place uncertainty surfaces.
- Confirmation that `dotnet build && dotnet test` passed.
- What's still manual: writing the first real spec with `/discover`.
