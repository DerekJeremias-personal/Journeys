### Task 2: Map schema and GraphQL request builders (no HTTP)

**Files:**
- Create: `scripts/linear-aidlc-projection.ps1` (builders + map IO only in this task)
- Create: `scripts/tests/linear-aidlc-projection.Tests.ps1`

**Interfaces:**
- Consumes: example YAML field names from Task 1
- Produces: `Read-LinearMap`, `Write-LinearMap`, `New-LinearGraphqlBody`, `Assert-UnitNotFrozen` for later HTTP verbs

- [ ] **Step 1: Write the failing tests first** â€” create `scripts/tests/linear-aidlc-projection.Tests.ps1`

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

Expected: FAIL (file missing or functions missing). If Pester is not installed, install for the user session only: `Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck` â€” do not change machine policy.

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
                query = 'query States($teamId: String!) { workflowStates(filter: { team: { id: { eq: $teamId } } }) { nodes { id name } } }'
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

and reader that parses those four keys per unit (do not pull in a new NuGet/npm dependency). Prefer ConvertFrom-Json of a sibling `linear-map.json` **only if** YAML parsing is impractical; the spec names `linear-map.yaml` â€” implement YAML with the small reader, not JSON.

- [ ] **Step 4: Re-run Pester**

```powershell
Invoke-Pester -Path .\scripts\tests\linear-aidlc-projection.Tests.ps1
```

Expected: PASS.

---

