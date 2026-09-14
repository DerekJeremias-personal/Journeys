# Task 3 Report: `upsert` and `pull-back`

**Status:** DONE_WITH_CONCERNS (implementation and tests in tree; Pester RED/GREEN could not be executed — Shell blocked by preToolUse hook)

**Date:** 2026-09-14

## Summary

Added `-Action upsert` and `-Action pull-back` on `scripts/linear-aidlc-projection.ps1` using injectable `Invoke-LinearGraphql` (`$script:LinearGraphqlInvoker` scriptblock replaces `Invoke-RestMethod`). Tests in `scripts/tests/linear-aidlc-projection.Tests.ps1` cover unit-list parsing, freeze-on-pull-back with a mock invoker, overlay write, empty `teamId`, create, and frozen upsert skip. No live Linear. `LINEAR_API_KEY` is read from process env only (never printed or written). Commits: none.

## Steps

### Step 1: Tests first — DONE

Kept Task 2 describes. Added brief + mock-invoker coverage:

| Describe / It | Source |
|---------------|--------|
| `Unit list parsing` / `parses unit names from edge block` | Brief (verbatim) |
| `Unit list parsing` / `parses unit names from fenced yaml block` | Brief (“fenced or bare”) |
| `Pull-back freeze` / `refuses Invoke-LinearPullBack when frozen` | Brief freeze (map+unit shortcut) |
| `Invoke-LinearPullBack` / `throws when unit is frozen` | Required mock-invoker freeze |
| `Invoke-LinearPullBack` / `throws when unit lacks issueId` | Brief pull-back fail-closed |
| `Invoke-LinearPullBack` / `writes overlay markdown and map title` | Overlay path + no `unit-of-work.md` rewrite |
| `Invoke-LinearUpsert` / empty `teamId`, create+map, skip frozen | Brief upsert |
| `Linear GraphQL invoker` / missing API key | Fail-closed key |

Script load remains `BeforeAll { . ...ps1 }` for Pester 5+/6.2.

### Step 2: Pester RED — BLOCKED

```powershell
Remove-Module Pester -ErrorAction SilentlyContinue
Import-Module Pester -MinimumVersion 5.0 -Force
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Working directory: `C:\Dev\Journeys\Journeys`

**Output:** none. Tool result: `Rejected: Shell blocked by preToolUse hook`.

Expected RED (not captured): missing `Get-AidlcUnitNamesFromDependencyMarkdown` / `Invoke-LinearPullBack` / `Invoke-LinearUpsert` before implementation.

### Step 3: HTTP seam and actions — DONE

| Item | Behavior |
|------|----------|
| `Invoke-LinearGraphql` | Empty key → `LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md`. If `$script:LinearGraphqlInvoker` is a scriptblock, call it instead of `Invoke-RestMethod`. Else POST `https://api.linear.app/graphql`. |
| Config | `aidlc/spaces/default/linear-projection.yaml` relative to Journeys root (parent of `scripts/`). Override `$script:LinearProjectionConfigPath` in tests. Fail if `teamId` empty. Small YAML reader (no nuget/npm). |
| `Get-AidlcUnitNamesFromDependencyMarkdown` | Bare `units:` / `- name:` or fenced `yaml` block containing `units:`. |
| `Get-UnitDescriptionFromUnitOfWork` | Heading section for that unit (title + body). |
| `upsert` | Resolve `states.todo` via `workflowStates`; `issueCreate` or `issueUpdate` to Todo; skip title/description/state when `frozen`; write `<record>/linear-map.yaml`. |
| `pull-back` | Fail if API key missing or any unit lacks `issueId`; `Assert-UnitNotFrozen`; `issue` query; write `<record>/inception/units-generation/unit-linear-copy/<unit>.md`; store `title` on the map; do not rewrite `unit-of-work.md`. |
| Dispatcher | `Invoke-LinearProjectionMain` switches on `-Action`. |

Task 2 builders + map IO kept. Tests never hit the network.

### Step 4: Pester GREEN — BLOCKED

Same Shell refusal. **No GREEN output captured.**

Parent should run from `C:\Dev\Journeys\Journeys`:

```powershell
Remove-Module Pester -ErrorAction SilentlyContinue
Import-Module Pester -MinimumVersion 5.0 -Force
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Expect Task 2 tests still passing plus new parse / freeze / upsert / pull-back tests. Do not require a live Linear key.

## Self-review

**In scope / correct:**

- Injectable GraphQL; dummy key + mock invoker in tests
- Overlay path and map path match the brief
- Frozen upsert does not `issueUpdate` / `issueCreate`
- Frozen pull-back throws `unit '…' is frozen; pull-back refused`
- No capability ids, no Linear polling, no `aidlc-sensor-*.ts`
- `LINEAR_API_KEY` never printed or written to disk

**Not done:** live Pester RED/GREEN (hook). Optional live smoke remains human-only.

**Also in tree (outside this task’s brief):** `claim` / `comment` / `complete` / `cancel` helpers and two Task 4 example tests (`Set-LinearClaimLocal`, attachment URL). Left in place so those tests keep passing; Task 4 can still own further coverage.

## Files changed

| File | Action |
|------|--------|
| `scripts/linear-aidlc-projection.ps1` | Modified (HTTP seam, upsert, pull-back, dispatcher) |
| `scripts/tests/linear-aidlc-projection.Tests.ps1` | Modified (parse, freeze-with-mock, upsert/pull-back) |

## Commits

None (per task instructions).

## Next action

Parent or user: run the Pester command above. Then Task 4 (`claim` / `comment` / `complete` / `cancel`) if not already complete.
