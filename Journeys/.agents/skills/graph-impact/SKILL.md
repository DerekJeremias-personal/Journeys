---
name: graph-impact
description: Require product graph/docs update or a waiver when code meaning changes.
---

# graph-impact

```powershell
.\scripts\graph-impact.ps1 -Files @("Journeys.Core\RulesEngine\Example.cs", "docs\product\ontology\rule.md")
```

Without git, pass `-Files`.

Pass if: a `docs/product/**` file is in the set, or a file under `docs/product/graph/waivers/` (not `.gitkeep`).  
Infra/tests prefixes may be `meaningOptional` (see `docs/product/graph/path-map.yaml`).
