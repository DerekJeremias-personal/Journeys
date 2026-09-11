---
name: agent-verify
description: Local gate — docs-impact, graph-impact, then dotnet build.
---

# agent-verify

```powershell
.\scripts\agent-verify.ps1 -Files @("Journeys.Core\Foo.cs", "docs\platform\architecture.md", "docs\product\ontology\campaign.md")
```

- Default: run both impact scripts, then `dotnet build Journeys.sln`.
- `-SkipImpact` — build only (not the definition of done).
- `-RunTests` or any `Journeys.Tests/**` path — also `dotnet test` that project.

Exit 0 only if every invoked step succeeds.
