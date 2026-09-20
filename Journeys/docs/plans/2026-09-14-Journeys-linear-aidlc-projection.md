# Journeys Linear AI-DLC Projection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. This repo’s human rule wins: do not start tasks until the user approves this plan. Prefer a fresh subagent per task.

**Goal:** Project AI-DLC units onto Linear as a human review/progress board (upsert at unit create, pull-back before construction, freeze on claim, Dev Complete + commit link) without making Linear the work queue.

**Architecture:** AI-DLC units remain canon. A PowerShell adapter calls Linear GraphQL. `.agents/skills/aidlc-journeys/SKILL.md` fail-closes construction until `pull-back` succeeds — same binding pattern as `scripts/aidlc-agent-verify-sensor.ps1`. No product capability ids. No shipped `aidlc-sensor-*.ts` fork.

**Tech Stack:** PowerShell 5+, Linear GraphQL (`https://api.linear.app/graphql`), existing `scripts/agent-verify.ps1`, Markdown/YAML config.

**Spec:** `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md`

## Global Constraints

- Do not invent capability ids. Existing ids only: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- Agents do not poll Linear for work. Status drags on Linear do not claim or complete units.
- Do not enable Linear Agent coding sessions as the Journeys implementer.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Do not copy `aidlc-workflows/core`. Do not add `aidlc-sensor-linear-*.ts` (harness requires `aidlc-sensor-<id>.ts` under `.cursor/tools/`).
- Secrets: `LINEAR_API_KEY` environment variable only. Never commit it.
- You still own git commit / push / merge unless the user asks in that message.
- Do not implement Playwright, agent merge, per-tenant Auth0, or pay-per-use billing.

## File map

| Path | Responsibility |
|------|----------------|
| `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md` | Design (already written) |
| `docs/developer/linear-aidlc-projection.md` | Operator contract: env, verbs, freeze, GitHub magic words |
| `docs/developer/index.md` | Link the operator doc |
| `docs/developer/local-ops.md` | `LINEAR_API_KEY` note |
| `aidlc/spaces/default/linear-projection.example.yaml` | Committed team/status names (no secrets) |
| `aidlc/spaces/default/linear-projection.yaml` | Local filled config (gitignored) |
| `.gitignore` | Ignore `linear-projection.yaml` |
| `scripts/linear-aidlc-projection.ps1` | `-Action upsert\|pull-back\|claim\|comment\|complete\|cancel` |
| `scripts/tests/linear-aidlc-projection.Tests.ps1` | GraphQL body + map freeze tests (no live Linear) |
| `.agents/skills/aidlc-journeys/SKILL.md` | When to run each verb; pull-back fail-closed |
| `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md` | No product meaning change |
| `<record>/linear-map.yaml` | Per-intent unit → Linear id (created at runtime, committed with the intent record) |

---

### Task 1: Operator docs and local config skeleton

**Files:**
- Create: `docs/developer/linear-aidlc-projection.md`
- Create: `aidlc/spaces/default/linear-projection.example.yaml`
- Modify: `docs/developer/index.md`
- Modify: `docs/developer/local-ops.md`
- Modify: `.gitignore`
- Create: `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md`

**Interfaces:**
- Consumes: spec §2, §7, §12
- Produces: operator doc and example YAML later tasks must not contradict

- [ ] **Step 1: Create `docs/developer/linear-aidlc-projection.md` with this exact body**

~~~~markdown
# Linear as AI-DLC unit projection

Phase 2 adapter. AI-DLC units stay canon. Linear is the human board.

**Spec:** `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md`

## Setup (human)

1. Linear team with statuses named exactly as in `aidlc/spaces/default/linear-projection.yaml`: `Todo`, `In Progress`, `In Review` (engine Dev Complete), `Done`.
2. Copy `aidlc/spaces/default/linear-projection.example.yaml` to `aidlc/spaces/default/linear-projection.yaml` and set `teamId` (Linear GraphQL id, not the issue prefix).
3. Set `LINEAR_API_KEY` in the environment. Do not commit it.
4. Optional: Linear GitHub integration + commit-linking webhook on `DerekJeremias-personal/Journeys`.

## Script

From `C:\Dev\Journeys\Journeys`:

```powershell
$env:LINEAR_API_KEY = "<key>"
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>"
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
.\scripts\linear-aidlc-projection.ps1 -Action claim -IntentRecordDir "<record>" -Unit "<unit-name>"
.\scripts\linear-aidlc-projection.ps1 -Action comment -IntentRecordDir "<record>" -Unit "<unit-name>" -Body "files: ... tests: ..."
.\scripts\linear-aidlc-projection.ps1 -Action complete -IntentRecordDir "<record>" -Unit "<unit-name>" -CommitSha "<sha>" -CommitUrl "https://github.com/DerekJeremias-personal/Journeys/commit/<sha>"
.\scripts\linear-aidlc-projection.ps1 -Action cancel -IntentRecordDir "<record>" -Unit "<unit-name>"
```

`<record>` is `aidlc/spaces/default/intents/<slug>-<id8>/`.

Exit 0 = success. Non-zero = fail closed (construction must not proceed).

## Freeze

After `claim`, `linear-map.yaml` has `frozen: true` for that unit. `pull-back` must refuse that unit. `upsert` may still push status and comments.

## Commits

Prefer `ref JOU-123` on in-progress commits. `Fixes JOU-123` only when merge should move the issue to Done. Agents do not commit unless asked in that message.
~~~~

- [ ] **Step 2: Add this bullet to `docs/developer/index.md` (keep existing bullets)**

```markdown
- [Linear AI-DLC projection](linear-aidlc-projection.md) — board for units; engine stays canon
```

- [ ] **Step 3: Append this subsection to `docs/developer/local-ops.md` after the existing secrets section**

```markdown
## Linear projection (optional)

The AI-DLC Linear board adapter reads `LINEAR_API_KEY` from the environment (not user secrets, not `appsettings`). See [linear-aidlc-projection.md](linear-aidlc-projection.md).
```

- [ ] **Step 4: Create `aidlc/spaces/default/linear-projection.example.yaml`**

```yaml
# Copy to linear-projection.yaml (gitignored) and fill teamId.
teamId: ""
teamKey: JOU
githubRepoUrl: https://github.com/DerekJeremias-personal/Journeys
states:
  todo: Todo
  inProgress: In Progress
  devComplete: In Review
  done: Done
  cancelled: Canceled
```

- [ ] **Step 5: Add this line to `.gitignore` next to the other `aidlc/` ignores**

```
aidlc/spaces/*/linear-projection.yaml
```

- [ ] **Step 6: Create `docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md`**

```markdown
# Waiver: Linear AI-DLC projection adapter

**Reason:** Scripts and developer docs for projecting AI-DLC units onto Linear. No product capability, ontology, or graph edge change. Humans still merge. Agents still claim units, not Linear tickets.

**Nodes touched by path-map (no meaning change):** none (scripts/ and docs/developer/ only).
```

- [ ] **Step 7: Confirm files exist**

Run:

```powershell
Test-Path docs\developer\linear-aidlc-projection.md
Test-Path aidlc\spaces\default\linear-projection.example.yaml
Test-Path docs\product\graph\waivers\2026-09-14-linear-aidlc-projection.md
Select-String -Path .gitignore -Pattern "linear-projection.yaml"
```

Expected: all `True` / a matching gitignore line.

---

### Task 2: Map schema and GraphQL request builders (no HTTP)

**Files:**
- Create: `scripts/linear-aidlc-projection.ps1` (builders + map IO only in this task)
- Create: `scripts/tests/linear-aidlc-projection.Tests.ps1`

**Interfaces:**
- Consumes: example YAML field names from Task 1
- Produces: `Read-LinearMap`, `Write-LinearMap`, `New-LinearGraphqlBody`, `Assert-UnitNotFrozen` for later HTTP verbs

- [ ] **Step 1: Write the failing tests first** — create `scripts/tests/linear-aidlc-projection.Tests.ps1`

The script under test will be dot-sourced. Until Task 2 Step 3 exists, this fails.

```powershell
$here = Split-Path -Parent $PSCommandPath
. (Join-Path (Split-Path $here) "linear-aidlc-projection.ps1")

Describe "Linear map freeze" {
    It "refuses pull-back when frozen" {
        $map = @{
            units = @{
                "u1-demo" = @{ identifier = "JOU-1"; issueId = "abc"; frozen = $true; title = "t" }
            }
        }
        { Assert-UnitNotFrozen -Map $map -Unit "u1-demo" } | Should -Throw
    }
}

Describe "GraphQL bodies" {
    It "issueCreate includes teamId title description stateId" {
        $body = New-LinearGraphqlBody -Operation issueCreate -TeamId "team-1" -Title "U1" -Description "AC" -StateId "state-todo"
        $json = $body | ConvertFrom-Json
        $json.variables.input.teamId | Should -Be "team-1"
        $json.variables.input.title | Should -Be "U1"
        $json.variables.input.stateId | Should -Be "state-todo"
    }
}
```

- [ ] **Step 2: Run tests and confirm they fail**

```powershell
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Expected: FAIL (file missing or functions missing). If Pester is not installed, install for the user session only: `Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck` — do not change machine policy.

- [ ] **Step 3: Implement builders in `scripts/linear-aidlc-projection.ps1`**

Put functions in this file. Do not call Linear yet. When the file is invoked as a script (`-Action`), it may parse params; when dot-sourced, skip the main dispatcher if `$MyInvocation.InvocationName -eq '.'` or if `-SkipMain` is set. Use this pattern at the bottom:

```powershell
if ($MyInvocation.InvocationName -ne '.' -and $PSBoundParameters.ContainsKey('Action')) {
    Invoke-LinearProjectionMain
}
```

Required functions (exact names):

```powershell
function Assert-UnitNotFrozen {
    param($Map, [string]$Unit)
    if ($Map.units[$Unit].frozen -eq $true) {
        throw "unit '$Unit' is frozen; pull-back refused"
    }
}

function New-LinearGraphqlBody {
    param(
        [ValidateSet("issueCreate","issueUpdate","commentCreate","attachmentCreate","workflowStates","issue")]
        [string]$Operation,
        [string]$TeamId,
        [string]$Title,
        [string]$Description,
        [string]$StateId,
        [string]$IssueId,
        [string]$Body,
        [string]$Url,
        [string]$AttachmentTitle
    )
    switch ($Operation) {
        "issueCreate" {
            return (@{
                query = 'mutation IssueCreate($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { id identifier url } } }'
                variables = @{ input = @{ teamId = $TeamId; title = $Title; description = $Description; stateId = $StateId } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "issueUpdate" {
            return (@{
                query = 'mutation IssueUpdate($id: String!, $input: IssueUpdateInput!) { issueUpdate(id: $id, input: $input) { success issue { id identifier } } }'
                variables = @{ id = $IssueId; input = @{ title = $Title; description = $Description; stateId = $StateId } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "commentCreate" {
            return (@{
                query = 'mutation CommentCreate($input: CommentCreateInput!) { commentCreate(input: $input) { success comment { id } } }'
                variables = @{ input = @{ issueId = $IssueId; body = $Body } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "attachmentCreate" {
            return (@{
                query = 'mutation AttachmentCreate($input: AttachmentCreateInput!) { attachmentCreate(input: $input) { success attachment { id } } }'
                variables = @{ input = @{ issueId = $IssueId; title = $AttachmentTitle; url = $Url } }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "workflowStates" {
            return (@{
                query = 'query States($teamId: ID!) { workflowStates(filter: { team: { id: { eq: $teamId } } }) { nodes { id name } } }'
                variables = @{ teamId = $TeamId }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
        "issue" {
            return (@{
                query = 'query Issue($id: String!) { issue(id: $id) { id identifier title description state { id name } } }'
                variables = @{ id = $IssueId }
            } | ConvertTo-Json -Depth 8 -Compress)
        }
    }
}

function Read-LinearMap {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{ units = @{} } }
    return ConvertFrom-YamlOrHashtable (Get-Content -Raw $Path)
}
```

If `powershell-yaml` is not available, store `linear-map.yaml` via a minimal hand-rolled writer that emits:

```yaml
units:
  u1-demo:
    identifier: JOU-1
    issueId: uuid
    frozen: false
    title: "..."
```

and reader that parses those four keys per unit (do not pull in a new NuGet/npm dependency). Prefer ConvertFrom-Json of a sibling `linear-map.json` **only if** YAML parsing is impractical; the spec names `linear-map.yaml` — implement YAML with the small reader, not JSON.

- [ ] **Step 4: Re-run Pester**

```powershell
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Expected: PASS.

---

### Task 3: `upsert` and `pull-back`

**Files:**
- Modify: `scripts/linear-aidlc-projection.ps1`
- Modify: `scripts/tests/linear-aidlc-projection.Tests.ps1`

**Interfaces:**
- Consumes: `New-LinearGraphqlBody`, map IO from Task 2
- Produces: `-Action upsert` and `-Action pull-back` using `Invoke-LinearGraphql` (injectable)

- [ ] **Step 1: Add tests for unit list parsing and freeze on pull-back**

Parse units from `<record>/inception/units-generation/unit-of-work-dependency.md` fenced yaml `units:` / `name:`. Test with a temp file:

```powershell
It "parses unit names from edge block" {
    $md = @"
units:
  - name: u1-demo
    kind: service
    depends_on: []
  - name: u2-other
    kind: spec
    depends_on: [u1-demo]
"@
    $names = Get-AidlcUnitNamesFromDependencyMarkdown -Markdown $md
    $names | Should -Be @("u1-demo", "u2-other")
}
```

Parser must accept either a fenced ```yaml block or a bare `units:` list.

Add a test that `Invoke-LinearPullBack` throws when `frozen` is true (call the function, do not hit the network).

- [ ] **Step 2: Run Pester — expect FAIL on new tests**

- [ ] **Step 3: Implement HTTP seam and actions**

```powershell
function Invoke-LinearGraphql {
    param([string]$Body, [string]$ApiKey)
    if (-not $ApiKey) { throw "LINEAR_API_KEY is unset. See docs/developer/linear-aidlc-projection.md" }
    Invoke-RestMethod -Method Post -Uri "https://api.linear.app/graphql" -Headers @{
        Authorization = $ApiKey
        "Content-Type" = "application/json"
    } -Body $Body
}

function Get-AidlcUnitNamesFromDependencyMarkdown {
    param([string]$Markdown)
    # Extract names under the fenced yaml units: list (name: lines).
}

function Get-UnitDescriptionFromUnitOfWork {
    param([string]$Markdown, [string]$Unit)
    # Return the section for that unit (title + body) for issue description.
}
```

`upsert`:

1. Fail if `linear-projection.yaml` `teamId` is empty.
2. Resolve state ids via `workflowStates` query; match `states.todo` name.
3. For each unit name, if map has `issueId` then `issueUpdate` title/description/state Todo unless frozen (if frozen, update state only when `-Action claim|complete|cancel`).
4. If no `issueId`, `issueCreate` with Todo.
5. Write `<record>/linear-map.yaml`.

`pull-back`:

1. Fail if API key missing or any unit lacks `issueId`.
2. For each unfrozen unit, `issue` query; write title + description back into that unit’s overlay file: `<record>/inception/units-generation/unit-linear-copy/<unit>.md` (create directory). Also store `title` on the map.
3. Do not rewrite `unit-of-work.md` topology/YAML. Construction reads `unit-linear-copy/<unit>.md` when present (skill in Task 5).
4. Refuse frozen units (`Assert-UnitNotFrozen`).

- [ ] **Step 4: Pester PASS for parse + freeze. Do not require a live Linear key in CI.**

Optional live smoke (human only): `-Action upsert` against a throwaway team.

---

### Task 4: `claim`, `comment`, `complete`, `cancel`

**Files:**
- Modify: `scripts/linear-aidlc-projection.ps1`
- Modify: `scripts/tests/linear-aidlc-projection.Tests.ps1`

**Interfaces:**
- Consumes: map `issueId`, state names `inProgress` / `devComplete` / `cancelled`
- Produces: freeze on claim; Dev Complete + attachment on complete

- [ ] **Step 1: Tests**

```powershell
It "claim sets frozen true in map object" {
    $map = @{ units = @{ "u1-demo" = @{ issueId = "x"; frozen = $false } } }
    $next = Set-LinearClaimLocal -Map $map -Unit "u1-demo"
    $next.units["u1-demo"].frozen | Should -Be $true
}

It "complete body includes attachment url" {
    $body = New-LinearGraphqlBody -Operation attachmentCreate -IssueId "x" -Url "https://github.com/DerekJeremias-personal/Journeys/commit/abc" -AttachmentTitle "commit abc"
    $body | Should -Match "github.com/DerekJeremias-personal/Journeys/commit/abc"
}
```

- [ ] **Step 2: Pester FAIL then implement**

`claim`: `issueUpdate` stateId = In Progress; set `frozen: true`; write map.

`comment`: `commentCreate`; no freeze change.

`complete`: require `-CommitSha`; `issueUpdate` to configured `states.devComplete` (In Review); `commentCreate` with SHA; `attachmentCreate` with `-CommitUrl` or `{githubRepoUrl}/commit/{sha}`.

`cancel`: `issueUpdate` cancelled state.

- [ ] **Step 3: Pester PASS**

---

### Task 5: Bind `aidlc-journeys` (fail-closed construction)

**Files:**
- Modify: `.agents/skills/aidlc-journeys/SKILL.md`
- Modify: `aidlc/spaces/default/memory/project.md` (one bullet under Way of Working)

**Interfaces:**
- Consumes: script verbs from Tasks 3–4
- Produces: conductor contract so pull-back cannot be skipped

- [ ] **Step 1: Append to `.agents/skills/aidlc-journeys/SKILL.md` after the existing numbered list**

~~~~markdown
5. Linear projection (Phase 2). Units are still the work source. After `units-generation` completes, run:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action upsert -IntentRecordDir "<record>"
```

Before the first construction unit claim, run:

```powershell
.\scripts\linear-aidlc-projection.ps1 -Action pull-back -IntentRecordDir "<record>"
```

Non-zero exit means halt. Do not claim a unit. After `aidlc-unit claim` (or equivalent), run `-Action claim -Unit <name>`. During construction, throttle `-Action comment`. When the unit passes `aidlc-agent-verify-sensor.ps1` and a commit SHA exists, run `-Action complete`. Read `<record>/inception/units-generation/unit-linear-copy/<unit>.md` when present — that text is post-review canon for AC.

Do not `list_issues` to pick work. Do not start Linear Agent coding sessions.
~~~~

Replace `<record>` in the skill with: resolve from the active intent under `aidlc/spaces/default/intents/` (the directory that contains `aidlc-state.md`).

- [ ] **Step 2: Add this bullet under `## Way of Working` in `aidlc/spaces/default/memory/project.md`**

```markdown
- Linear is a projection of AI-DLC units (see `docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md` and `docs/developer/linear-aidlc-projection.md`). Do not poll Linear for work.
```

- [ ] **Step 3: Read the skill and project.md and confirm the verbs and fail-closed sentence are present**

---

### Task 6: Verify docs/graph gates

**Files:**
- (no new product code)

**Interfaces:**
- Consumes: waiver from Task 1, developer docs
- Produces: green `docs-impact` / `graph-impact` for this change set

- [ ] **Step 1: Run graph-impact with the waiver in the file set**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "docs/developer/linear-aidlc-projection.md",
  "docs/developer/index.md",
  "docs/developer/local-ops.md",
  "docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md",
  "docs/plans/2026-09-14-Journeys-linear-aidlc-projection.md",
  "docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md",
  "scripts/linear-aidlc-projection.ps1",
  ".agents/skills/aidlc-journeys/SKILL.md"
)
```

Expected: exit 0.

- [ ] **Step 2: Run docs-impact on the same `-Files` list**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "docs/developer/linear-aidlc-projection.md",
  "docs/developer/index.md",
  "docs/developer/local-ops.md",
  "docs/specs/2026-09-14-Journeys-linear-aidlc-projection-design.md",
  "docs/plans/2026-09-14-Journeys-linear-aidlc-projection.md",
  "docs/product/graph/waivers/2026-09-14-linear-aidlc-projection.md",
  "scripts/linear-aidlc-projection.ps1",
  ".agents/skills/aidlc-journeys/SKILL.md"
)
```

Expected: exit 0 (`scripts/` is not in `path-docs-map.yaml`; developer docs are in `-Files`).

- [ ] **Step 3: Re-run Pester**

```powershell
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Expected: PASS.

- [ ] **Step 4: Do not commit** unless the user asks in that message.

---

## Self-review (plan vs spec)

| Spec goal | Task |
|-----------|------|
| G1 issue at unit create | Task 3 `upsert` |
| G2 pull-back before construction | Task 3 `pull-back` + Task 5 skill |
| G3 freeze after claim | Task 4 `claim` + Task 2 `Assert-UnitNotFrozen` |
| G4 comments | Task 4 `comment` |
| G5 Dev Complete + commit | Task 4 `complete` |
| G6 human merge | Global constraints; no merge automation in script |
| Non-goal: no poller | Task 5 skill |
| Non-goal: no sensor fork | Global constraints |
| Open at install: teamId / API key | Task 1 example yaml + local-ops |

No `TBD` remaining except Linear `teamId`, which is an install-time fill-in of `linear-projection.yaml`, not a design fork.
