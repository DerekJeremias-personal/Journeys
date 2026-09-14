### Task 5: Install AI-DLC Cursor harness

**Files:** installer-managed: `.cursor/**` (merge), `aidlc/**` (new), marked sections in `AGENTS.md`. Do not hand-edit `C:\Dev\AI\AIDLC\aidlc-workflows\dist*`.

**Interfaces:**
- Consumes: Phase 0 complete (Tasks 1â€“4)
- Produces: `aidlc` workspace shell + Cursor harness files for Task 6

- [ ] **Step 1: Confirm Phase 0 files exist**

```powershell
@(
  "docs\product\ontology\outcome.md",
  "docs\product\ontology\loyalty-account.md",
  "docs\platform\security.md",
  "docs\platform\runtime.md",
  "docs\developer\logging.md",
  "docs\developer\coding-standards.md"
) | ForEach-Object { if (-not (Test-Path $_)) { throw "missing $_" } }
Write-Host "phase0: OK"
```

Expected: `phase0: OK`.

- [ ] **Step 2: Snapshot Journeys-owned rules before install**

```powershell
Get-ChildItem .cursor\rules\*.mdc | Select-Object -ExpandProperty Name | Sort-Object
```

Expected names include: `session-context.mdc`, `docs-and-graph-sync.mdc`, `onion-architecture.mdc`, `api-controllers.mdc`, `core-services.mdc`, `dal-adapters.mdc`, `infra-adapters.mdc`, `tests.mdc`, `campaign-agent.mdc`. Save this list; Task 5 Step 5 must still see all of them.

- [ ] **Step 3: Ensure `aidlc` is on PATH**

```powershell
Get-Command aidlc -ErrorAction SilentlyContinue
```

If missing, install the native command (Windows):

```powershell
irm https://github.com/awslabs/aidlc-workflows/releases/latest/download/install.ps1 | iex
```

If that release is still v1-only or `aidlc` still missing, stop and report â€” do not copy `core/` into this sln. Do not run `bun scripts/package.ts` inside Journeys.

- [ ] **Step 4: Configure this project**

From `C:\Dev\Journeys\Journeys`:

```powershell
aidlc config --harness cursor
aidlc doctor
```

Expected: doctor exits 0 (or prints a fixable PATH/hook warning you resolve without deleting Journeys rules).

- [ ] **Step 5: Collision check**

```powershell
$required = @(
  "session-context.mdc","docs-and-graph-sync.mdc","onion-architecture.mdc",
  "api-controllers.mdc","core-services.mdc","dal-adapters.mdc",
  "infra-adapters.mdc","tests.mdc","campaign-agent.mdc"
)
$have = Get-ChildItem .cursor\rules\*.mdc | Select-Object -ExpandProperty Name
$missing = $required | Where-Object { $_ -notin $have }
if ($missing) { throw "installer removed Journeys rules: $($missing -join ', ')" }
Select-String -Path AGENTS.md -Pattern "Essential reading","Human-only authorities","signal response engine" | Measure-Object | Select-Object -ExpandProperty Count
Test-Path aidlc
```

Expected: no throw; Select-String count â‰¥ 3; `aidlc` path True. If the installer overwrote the reading order, restore those `AGENTS.md` sections from git and keep any AI-DLC marked block **below** the Journeys contract. Do not commit.

---

