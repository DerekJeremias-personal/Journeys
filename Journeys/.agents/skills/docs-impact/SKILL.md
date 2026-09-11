---
name: docs-impact
description: Require platform/developer docs in the same change as code.
---

# docs-impact

From `C:\Dev\Journeys\Journeys`:

```powershell
.\scripts\docs-impact.ps1 -Files @("Journeys.Core\Services\Example.cs", "docs\platform\architecture.md")
```

If there is no `.git`, `-Files` is required (the full change set).

Exit 0: every mapped code path has its required docs in `-Files`.  
Exit 1: print missing `code -> doc` pairs.

Map: `scripts/path-docs-map.yaml`.
