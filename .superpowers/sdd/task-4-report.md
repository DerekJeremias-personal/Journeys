# Task 4 Report: Graph, path maps, impact scripts

**Status:** DONE  
**Date:** 2026-09-13  
**Commits:** none (per brief)

## Summary

Phase 0 graph wiring for outcomes: appended three edges (process-event → outcomes, outcomes → proj-core / proj-notification), inserted two longest-prefix path-map entries, and replaced `scripts/path-docs-map.yaml` with the specified mapping. All existing edges and RulesEngine/Core path-map entries were kept. No capability ids added. No product C# changed.

## Files modified

### `docs/product/graph/edges.yaml`

Kept all existing edges, including `campaigns SERVES outcomes`. Appended:

- `process-event REALIZED_BY outcomes`
- `outcomes IMPLEMENTED_AS proj-core`
- `outcomes IMPLEMENTED_AS proj-notification`

### `docs/product/graph/path-map.yaml`

Inserted at the top of `entries:`:

- `Journeys.Core/RulesEngine/Outcomes` → `[outcomes]` (`meaningOptional: false`)
- `Journeys.Core/Models` → `[outcomes, campaigns]` (`meaningOptional: false`)

Left existing `Journeys.Core/RulesEngine` and `Journeys.Core` entries in place.

### `scripts/path-docs-map.yaml`

Replaced with the exact body from the brief (Outcomes + Models prefixes first; Infra/Notification docs extended; Journeys.API now also maps `docs/platform/security.md`).

## Files not modified

- `docs/product/graph/nodes.yaml` — no Capability or UseCase nodes added
- Product C# — none
- aidlc config — not run

## Verification

### Step 4: docs-impact fails without the new ontology

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md"
)
```

```
docs-impact: FAIL - required docs not in the change set:
  Journeys.Core/RulesEngine/Outcomes/DepositPointsOutcome.cs -> docs/product/ontology/outcome.md
EXIT:1
```

Expected: exit 1 and a line containing `docs/product/ontology/outcome.md`. **Met.**

### Step 5: docs-impact passes with the mapped docs

```powershell
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\platform\architecture.md",
  "docs\product\ontology\outcome.md"
)
```

```
docs-impact: OK (1 files)
EXIT:0
```

Expected: exit 0, `docs-impact: OK`. **Met.**

### Step 6: graph-impact fails without a product update

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs"
)
```

```
graph-impact: FAIL - nodes outcomes need a docs/product update or waiver
EXIT:1
```

Expected: exit 1, nodes include `outcomes`. **Met.**

### Step 7: graph-impact passes with a product file in the set

```powershell
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.Core\RulesEngine\Outcomes\DepositPointsOutcome.cs",
  "docs\product\ontology\outcome.md"
)
```

```
graph-impact: OK nodes=outcomes productUpdate=True waiver=False
EXIT:0
```

Expected: exit 0. **Met.**

### Step 8: Confirm no new capability ids

```powershell
Select-String -Path docs\product\graph\nodes.yaml -Pattern "label: Capability"
```

```
docs\product\graph\nodes.yaml:3:    label: Capability
docs\product\graph\nodes.yaml:6:    label: Capability
docs\product\graph\nodes.yaml:9:    label: Capability
docs\product\graph\nodes.yaml:12:    label: Capability
docs\product\graph\nodes.yaml:15:    label: Capability
docs\product\graph\nodes.yaml:18:    label: Capability
docs\product\graph\nodes.yaml:21:    label: Capability
```

Same seven capability ids as before: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`. **Met.**

## Scope compliance

- No product C# code changed
- No new capability ids invented
- Existing `edges.yaml` entries kept; three edges appended only
- Existing RulesEngine and Core path-map entries left in place
- No aidlc config run
- No git commit, push, merge, or PR

## Concerns

None.
