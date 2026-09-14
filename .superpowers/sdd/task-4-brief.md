### Task 4: Graph, path maps, impact scripts

**Files:**
- Modify: `docs/product/graph/edges.yaml`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`

**Interfaces:**
- Consumes: Task 2 ontology files; existing `nodes.yaml` (do not add capability ids)
- Produces: impact scripts exit 0/1 as specified

- [ ] **Step 1: Append these edges to `docs/product/graph/edges.yaml` (keep all existing edges)**

```yaml
  - from: process-event
    type: REALIZED_BY
    to: outcomes
  - from: outcomes
    type: IMPLEMENTED_AS
    to: proj-core
  - from: outcomes
    type: IMPLEMENTED_AS
    to: proj-notification
```

Do not add a new Capability or UseCase node. Do not remove `campaigns SERVES outcomes`.

- [ ] **Step 2: Insert these two entries at the top of `entries:` in `docs/product/graph/path-map.yaml` (longest prefix must be listed; matcher already uses longest prefix)**

```yaml
  - prefix: Journeys.Core/RulesEngine/Outcomes
    nodes: [outcomes]
    meaningOptional: false
  - prefix: Journeys.Core/Models
    nodes: [outcomes, campaigns]
    meaningOptional: false
```

Leave existing `Journeys.Core/RulesEngine` and `Journeys.Core` entries in place.

- [ ] **Step 3: Insert these entries at the top of `scripts/path-docs-map.yaml` and extend Infra/Notification**

Replace the file with:

```yaml
entries:
  - prefix: Journeys.Core/RulesEngine/Outcomes
    docs: [docs/platform/architecture.md, docs/product/ontology/outcome.md]
  - prefix: Journeys.Core/Models
    docs: [docs/platform/architecture.md, docs/product/ontology/loyalty-account.md, docs/product/ontology/outcome.md]
  - prefix: Journeys.API/CampaignAgent
    docs: [docs/platform/architecture.md, docs/developer/tools.md]
  - prefix: Journeys.API/Mcp
    docs: [docs/platform/architecture.md, docs/developer/index.md]
  - prefix: Journeys.API
    docs: [docs/platform/architecture.md, docs/platform/security.md]
  - prefix: Journeys.Core/RulesEngine
    docs: [docs/platform/architecture.md, docs/product/ontology/rule.md]
  - prefix: Journeys.Core
    docs: [docs/platform/architecture.md, docs/product/ontology/campaign.md]
  - prefix: Journeys.DTO
    docs: [docs/platform/architecture.md]
  - prefix: Journeys.DAL
    docs: [docs/platform/architecture.md]
  - prefix: Journeys.Agent
    docs: [docs/platform/architecture.md, docs/developer/tools.md]
  - prefix: Journeys.Tests
    docs: [docs/developer/testing.md]
  - prefix: Journeys.Infra
    docs: [docs/platform/architecture.md, docs/platform/runtime.md, docs/platform/security.md]
  - prefix: Journeys.Notification
    docs: [docs/platform/architecture.md, docs/product/ontology/outcome.md]
```

- [ ] **Step 4: Prove docs-impact fails without the new ontology**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md"
)
```

Expected: exit 1 and a line containing `docs/product/ontology/outcome.md`.

- [ ] **Step 5: Prove docs-impact passes with the mapped docs**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md",
  "docs\product\ontology\outcome.md"
)
```

Expected: exit 0, `docs-impact: OK`.

- [ ] **Step 6: Prove graph-impact fails without a product update**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs"
)
```

Expected: exit 1, nodes include `outcomes`.

- [ ] **Step 7: Prove graph-impact passes with a product file in the set**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\product\ontology\outcome.md"
)
```

Expected: exit 0.

- [ ] **Step 8: Confirm no new capability ids**

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern "label: Capability"
```

Expected: the same seven capability ids as before (`event-models` through `mcp-api`). Do not commit.

---

