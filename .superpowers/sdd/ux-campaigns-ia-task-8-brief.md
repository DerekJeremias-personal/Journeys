### Task 8: Docs, graph, browser pass

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/campaign-agent-llm.md` (one line: UX rail hydrates from SSE; provider unchanged)
- Modify: `docs/product/graph/path-map.yaml` â€” `Journeys.UX` nodes: `[campaigns, journeys, campaign-agent]`
- Modify: `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` status â†’ Approved after implementation (human still merges)
- Modify: `docs/platform/architecture.md` only if docs-impact requires a sentence that UX now mutates campaigns over HTTP (UX is still not write authority)

**Interfaces:**
- Consumes: shipped behavior from Tasks 1â€“7
- Produces: docs-impact / graph-impact clean on the implementation file set

- [ ] **Step 1: Update `journeys-ux.md`**

Document: campaigns are mutable; routes in spec Â§6; copy/restore; Unpublish=Pause; unlabeled agent data plane; collapsed Campaign JSON; no Campaign AdminAudit this increment; Tailwind is Journeys-neutral.

- [ ] **Step 2: `path-map.yaml`**

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, journeys, campaign-agent]
    meaningOptional: false
```

Do **not** add `IMPLEMENTED_AS` `proj-ux` on `campaigns`.

- [ ] **Step 3: Verify**

```powershell
cd C:\Dev\Journeys\Journeys
.\scripts\agent-verify.ps1 -Files @(
  "Journeys.UX/src/lib/map-loyalty-path.ts",
  "Journeys.Core/Services/CampaignService.cs",
  "Journeys.API/Controllers/CampaignController.cs",
  "docs/developer/journeys-ux.md",
  "docs/product/graph/path-map.yaml"
) -RunTests
```

From `Journeys.UX`: `npm test`.

Expected: both OK.

- [ ] **Step 4: Browser pass** (human or browser tools; not CI)

1. Sign in â†’ Campaigns cards/filters.
2. Kebab: Publish confirm; Unpublish results in **pause**; Duplicate hits copy (new id + new ext); Delete Draft-only.
3. New: builder + agent rail; one SSE turn; builder hydrates or conflict; inspector collapsed then expands GET JSON.
4. Wizard edit; Live edit does not upsert Live journey in place.
5. Archive list restore; versions page.
6. Live-only `/[id]/agent`; non-live redirects.
7. Resume link when conversations exist.

Do not commit unless asked.

---

