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

- [ ] **Step 2: Run Pester â€” expect FAIL on new tests**

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
2. For each unfrozen unit, `issue` query; write title + description back into that unitâ€™s overlay file: `<record>/inception/units-generation/unit-linear-copy/<unit>.md` (create directory). Also store `title` on the map.
3. Do not rewrite `unit-of-work.md` topology/YAML. Construction reads `unit-linear-copy/<unit>.md` when present (skill in Task 5).
4. Refuse frozen units (`Assert-UnitNotFrozen`).

- [ ] **Step 4: Pester PASS for parse + freeze. Do not require a live Linear key in CI.**

Optional live smoke (human only): `-Action upsert` against a throwaway team.

---

