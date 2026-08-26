#!/bin/bash
# ──────────────────────────────────────────────────────────────────────
# File Protection Hook (PreToolUse)
#
# Blocks Claude from editing files that shouldn't change during
# normal development: production config, secrets, CI pipelines,
# and Claude's own guardrails.
#
# ADAPTING FOR YOUR PROJECT:
#   Add or remove patterns in PROTECTED_PATTERNS to match your project.
#   Common additions:
#     "Dockerfile"          — if you don't want Claude touching containers
#     "docker-compose"      — same for compose files
#     "terraform/"          — infrastructure-as-code
#     "k8s/"                — Kubernetes manifests
#     ".gitlab-ci"          — GitLab CI (instead of .github/workflows)
#     "azure-pipelines"     — Azure DevOps pipelines
#     "packages.lock.json"  — if you manage lock files manually
#
#   Pattern matching is substring-based (*pattern*), not glob.
#   A pattern of ".env" matches ".env", ".env.local", ".env.production".
#
#   Exit codes:
#     0 — allow the edit
#     2 — block the edit (Claude sees the error and tries another approach)
#
# WHAT THIS CHECKS:
#   Edit/Write pass a `file_path`; Bash passes a `command`. Both are checked,
#   because a hook that only looked at `file_path` would wave through
#   `echo x > appsettings.Production.json` — the matcher in settings.json can
#   say `Edit|Write|Bash` and still protect nothing.
#
#   The Bash check is deliberately blunt: if a protected pattern appears
#   anywhere in the command text, the command is blocked, read-only ones
#   (`cat .env`) included — reading a secret is exactly what should be
#   stopped. It is a guardrail against honest mistakes, not a sandbox: a
#   determined command can always obscure a path (variables, globs, base64).
#   For hard guarantees use `permissions.deny` in settings.json, which the
#   harness enforces itself.
#
#   NOTE on `.claude/`: it is deliberately NOT in the list below. The harness
#   is protected by `permissions.deny` on Edit/Write in settings.json, which
#   leaves Bash free to install and adapt it — that's what `/bootstrap` needs.
#   Adding `.claude` here would close that door permanently, including on the
#   bootstrap skill itself.
# ──────────────────────────────────────────────────────────────────────

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

# ADAPT: Add patterns for files Claude should never edit in your project
PROTECTED_PATTERNS=(
  "appsettings.Production" # prod config
  ".env"                   # secrets
  ".pem" ".key" ".pfx"     # credentials & certificates
  ".github/workflows"      # CI pipelines
)

for pattern in "${PROTECTED_PATTERNS[@]}"; do
  # Edit / Write — the tool names its target outright.
  if [[ -n "$FILE_PATH" && "$FILE_PATH" == *"$pattern"* ]]; then
    echo "Blocked: $FILE_PATH matches protected pattern '$pattern'" >&2
    exit 2
  fi

  # Bash — no file_path, so inspect the command text instead.
  if [[ -n "$COMMAND" && "$COMMAND" == *"$pattern"* ]]; then
    echo "Blocked: command references protected pattern '$pattern'." >&2
    echo "If this file genuinely needs to change, the user should edit it themselves." >&2
    exit 2
  fi
done
exit 0
