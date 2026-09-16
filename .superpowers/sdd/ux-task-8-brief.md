### Task 8: Verify impact scripts + local smoke

**Files:** none new.

- [ ] **Step 1: From `C:\Dev\Journeys\Journeys`**

```powershell
$files = @(
  "Journeys.UX/package.json",
  "docs/developer/journeys-ux.md",
  "docs/platform/architecture.md",
  "docs/platform/overlays.md",
  "docs/product/graph/nodes.yaml",
  "docs/product/graph/edges.yaml",
  "docs/product/graph/path-map.yaml",
  "scripts/path-docs-map.yaml"
)
.\scripts\docs-impact.ps1 -Files $files
.\scripts\graph-impact.ps1 -Files $files
```

Expected: both exit 0.

- [ ] **Step 2: `dotnet build .\Journeys.sln`** — exit 0 (UX is not in the sln).

- [ ] **Step 3: `cd Journeys.UX; npm test`** — all PASS.

- [ ] **Step 4: Manual (human):** API up, `.env.local` with `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true`, sign-in with key + tenant, open Overview, Campaigns, Accounts.

- [ ] **Step 5: Do not commit** unless the user asks in that message.
