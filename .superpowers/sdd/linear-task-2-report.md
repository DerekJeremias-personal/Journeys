# Task 2 Report: Map schema and GraphQL request builders (no HTTP)

**Status:** DONE_WITH_CONCERNS (implementation complete; Pester RED/GREEN could not be executed — Shell blocked by preToolUse hook)

**Date:** 2026-09-14

## Summary

Created `scripts/linear-aidlc-projection.ps1` (builders + `linear-map.yaml` IO only) and `scripts/tests/linear-aidlc-projection.Tests.ps1`. No Linear HTTP. No product capability ids. `LINEAR_API_KEY` is never read, printed, or written. Commits: none.

Tests were written first (Step 1). Shell (`Invoke-Pester`) was refused by the AI-DLC `preToolUse` guard on every attempt, including after `request_smart_mode_approval`. The same block hit Task 1. Parent/user must run Pester locally for RED/GREEN evidence.

## Steps

### Step 1: Failing tests first — DONE

Created `scripts/tests/linear-aidlc-projection.Tests.ps1` **before** the implementation script.

Verbatim from the brief:

- `Describe "Linear map freeze"` / `It "refuses pull-back when frozen"`
- `Describe "GraphQL bodies"` / `It "issueCreate includes teamId title description stateId"`

Added (allowed): `Describe "Linear map IO"` with round-trip, missing-file, and frozen-true cases for `Write-LinearMap` / `Read-LinearMap`.

At this point `scripts/linear-aidlc-projection.ps1` did not exist. Dot-sourcing in the test file would fail.

### Step 2: Run tests and confirm they fail — BLOCKED (no Pester output)

Command attempted (twice; second with smart-mode approval):

```powershell
if (-not (Get-Module -ListAvailable -Name Pester)) { Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck }; Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Working directory: `C:\Dev\Journeys\Journeys`

**Output:** none. Tool result: `Rejected: Shell blocked by preToolUse hook` (Agent note: do not suggest workarounds).

**Expected RED (not captured):** FAIL because `linear-aidlc-projection.ps1` was missing, so `. (Join-Path ...)` would throw (file not found / command not found for `Assert-UnitNotFrozen` / `New-LinearGraphqlBody`).

### Step 3: Implement builders — DONE

Created `scripts/linear-aidlc-projection.ps1`:

| Item | Implementation |
|------|----------------|
| `param()` + `$ErrorActionPreference = "Stop"` | Top of file, matching existing scripts |
| `Assert-UnitNotFrozen` | Exact throw from brief |
| `New-LinearGraphqlBody` | Exact GraphQL strings and operations from brief (`issueCreate`, `issueUpdate`, `commentCreate`, `attachmentCreate`, `workflowStates`, `issue`) |
| `Read-LinearMap` | Missing file → `@{ units = @{} }`; else `ConvertFrom-YamlOrHashtable (Get-Content -Raw $Path)` |
| `Write-LinearMap` | Hand-rolled YAML writer (four keys per unit; `frozen: true/false`; quoted `title`) |
| `ConvertFrom-YamlOrHashtable` | Private helper used by `Read-LinearMap`; also accepts a hashtable passthrough |
| Dispatcher | Skip main when dot-sourced (`InvocationName -eq '.'`) or `-SkipMain`; run `Invoke-LinearProjectionMain` only when `-Action` is bound |
| `Invoke-LinearProjectionMain` | Stub that **throws** (no HTTP). Later tasks replace this. |

YAML shape written (no JSON, no powershell-yaml / NuGet / npm):

```yaml
units:
  u1-demo:
    identifier: JOU-1
    issueId: uuid
    frozen: false
    title: "..."
```

UTF-8 **without BOM** so `(?m)^units:` matches.

### Step 4: Re-run Pester — BLOCKED (no Pester output)

Same command as Step 2. Same hook refusal. **No GREEN output captured.**

**Expected GREEN (not captured):** all Describe/It blocks PASS (2 brief tests + 3 map IO tests).

Parent should run from `C:\Dev\Journeys\Journeys`:

```powershell
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

If Pester is missing:

```powershell
Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck
```

## Self-review

**In scope / correct:**

- Builders + map IO only; no `Invoke-RestMethod` / Linear GraphQL HTTP
- Function names match the brief exactly
- GraphQL query strings match the brief (single-quoted so `$input` / `$id` / `$teamId` are literal)
- Map file is YAML named `linear-map.yaml`, not JSON
- No new dependencies
- No capability ids invented
- `LINEAR_API_KEY` never referenced
- Tests keep the brief's Describe/It blocks verbatim
- `param()` at top; PowerShell 5+ constructs only
- Dot-source path used by tests skips `Invoke-LinearProjectionMain`

**Intentional stub:**

- `-Action` dispatcher exists so later tasks can fill HTTP verbs; calling it now throws `"Linear HTTP actions are not implemented yet"`

**Not done in this task (correctly omitted):**

- `upsert` / `pull-back` / `claim` / `comment` / `complete` / `cancel` HTTP
- `Invoke-LinearGraphql`
- Skill binding (Task 5)
- `docs-impact` / `graph-impact` live run (Task 6; Shell also blocked). Waiver already exists from Task 1: `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md`. `scripts/` is not in `path-docs-map.yaml`.

**Risks to re-check after Pester is runnable:**

- Windows PowerShell 5.1 `ConvertTo-Json` key order is undefined; tests assert property values, not JSON text order
- Hand-rolled YAML reader only understands this four-key indent shape (sufficient for the spec)

## Files changed

| File | Action |
|------|--------|
| `scripts/tests/linear-aidlc-projection.Tests.ps1` | Created (tests first) |
| `scripts/linear-aidlc-projection.ps1` | Created (after tests) |

## Commits

None (per task instructions).

## Next action

Parent or user: run `Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1` from `C:\Dev\Journeys\Journeys`. Then Task 3 (`upsert` / `pull-back`).
