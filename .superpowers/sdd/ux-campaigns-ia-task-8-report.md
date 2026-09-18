# Task 8 report: Docs, graph, browser pass

## Status

**DONE_WITH_CONCERNS**

No commit, push, merge, or PR.

## Docs/graph changes

| Path | Change |
|------|--------|
| `docs/developer/journeys-ux.md` | Campaigns mutable over HTTP (Core remains write authority). Fixed §6 route table (prose after `/new` no longer splits later rows). Copy/restore, Unpublish=Pause, unlabeled agent data plane, collapsed Campaign JSON, no Campaign AdminAudit this increment, Journeys-neutral Tailwind. |
| `docs/developer/campaign-agent-llm.md` | UX rail hydrates from SSE; provider unchanged. |
| `docs/product/graph/path-map.yaml` | `Journeys.UX` nodes: `[campaigns, journeys, campaign-agent]`. Did **not** add `IMPLEMENTED_AS` `proj-ux` on `campaigns`. |
| `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` | Status → Approved after implementation (human still merges). |
| `docs/platform/architecture.md` | UX may mutate campaigns over Campaign REST; still not write authority. |
| `docs/product/ontology/draft-live.md` | Copy vs restore vs Unpublish=Pause (Task 2 docs deferred here). |

## Verify

`docs-impact` / `graph-impact` required extra mapped docs in `-Files` (architecture, overlays, campaign, draft-live, security, tenant, campaign-agent-llm). Brief’s five-path list would fail `path-docs-map`.

```
docs-impact: OK (3 files)
graph-impact: OK nodes=campaigns, journeys, campaign-agent, mcp-api productUpdate=True waiver=False
```

`.\scripts\agent-verify.ps1 -Files @(...) -RunTests` then **failed the solution Debug build** (not impact): MSB3027 copy into `Journeys.API\bin\Debug` locked by `Journeys.API (13612)` and Visual Studio. Same lock as Task 2. Process left running for the browser pass.

Isolated tests (avoid the lock):

```
dotnet test .\Journeys.Tests\Journeys.Tests.csproj -p:BaseOutputPath=C:\Dev\Journeys\Temp\task8-verify\ --filter "FullyQualifiedName~CampaignCopyFactoryTests|FullyQualifiedName~CampaignServiceCopyRestoreTests|FullyQualifiedName~CampaignController"
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

`npm test` in `Journeys.UX`: **31 files, 167 tests passed**.

## Browser pass (localhost:3000, already signed in)

1. **Campaigns cards/filters — PASS.** Live card `testtenant1 tier system` (`tiersystem`). Name/From/To/Search present. Filter `tier` + Search used getmany `STARTSWITH(c.name, @name)` and returned no rows (name does not start with `tier`); Clear restored the card.
2. **Kebab — PARTIAL.** Live kebab: Edit, Open in Agent, Duplicate, Versions, Unpublish, Archive; **no Delete**, **no Publish** (Delete Draft-only visibility **PASS**). **Duplicate:** `copyCampaign(id, "live")` ran twice (~22–28ms); no toast, no new card (`getall` still only the Live row). **Unpublish:** save returned toast `An unexpected error occurred while saving the campaign.`; badge stayed **Live** (did not Pause). **Publish confirm:** skip — no Draft/Pause card on the list to open the dialog. Did not click Restore (archive empty).
3. **New builder + agent — PASS with hydrate skip.** Builder canvas + unlabeled rail. SSE turn completed (~54s): Conversation `412c6be8b1de45708eaea74a4464d605`; assistant `{"name": "ListCampaigns", "errors": ["Failed to call list_campaigns"]}`. Not API-down BLOCKED. No discovered campaign id → hydrate/conflict **skip**. Inspector not on New until linked. Live `/[id]/agent`: Campaign JSON collapsed, then expand showed GET JSON (`extCampaignId: tiersystem`, `status: live`) + Copy.
4. **Wizard edit — PASS.** `/loyalty/campaigns/{id}?campaignStatus=live` opens wizard; copy: “Saving creates a new draft with the same program identity.” Did not save (would create a new Draft).
5. **Archive / versions — PASS (empty / API message).** Archived: “No archived campaigns found.” Restore not clicked. Versions `/versions/tiersystem`: “No campaign versions found for ExtCampaignId 'tiersystem'.”
6. **Live-only `/[id]/agent` — PASS.** `?campaignStatus=live` stays on Agent. `?campaignStatus=draft` **307** → wizard `?campaignStatus=draft` (“Wizard edit requires a campaign partition.” — no Draft for that id).
7. **Resume — PARTIAL.** List had only **Agent**, not **Resume agent**, after the turn (`listAgentConversations` did not surface a row). Direct `/loyalty/campaigns/agent?conversationId=412c6be8…` showed Conversation id + copy control (history empty).

## Concerns

1. **Solution Debug `agent-verify` cannot copy while `Journeys.API` is running** (PID 13612). Impact scripts passed; isolated tests passed.
2. **List `getall` showed only the Live card.** Duplicate/Start-blank Drafts never appeared, so Publish confirm and Delete-on-Draft could not be clicked. Name filter is startswith, not contains.
3. **Unpublish save 500** — Live not paused. Copy server action returned in ~25ms with no UI result.
4. **SSE tool `list_campaigns` failed**; hydrate not exercised. Resume list link missing though a conversation id exists.
5. Prior minors unchanged: inspector/resume/live-route lack component tests; versions 404-as-error; archived kebab Edit no-op; new-draft status select still offers Live/Pause/Archive.

## Browser-pass fix follow-up (no commit)

### What was wrong

1. **Duplicate:** kebab already called `copyCampaign`. `getall` is Live-partition-only, so a new Draft never appeared. Copy itself is ~25–35ms because the **running** `Journeys.API` returns **empty HTTP 404** on `POST .../copy` (route not in the loaded Debug process). GET-by-id JSON 404s for some query-found drafts (point-read PK vs query). Slim copy response + toast path were also fragile if the API returned a large/empty body.
2. **Unpublish:** kebab mapped to `pause`, but `updateCampaign({ ...liveCard, status: "pause" })` resent the full Live journey through `CampaignDefinitionValidator` → generic save 500. Adapter already supports Live→Pause via `MoveEntityAsync` when journey is omitted.
3. **Resume:** `listAgentConversations` required camelCase `conversationId`. Live GET is actually `{"items":[]}` (camelCase, empty) — parse was one bug; empty AgentMessage list is why Resume still hides.

### What changed (UX)

- List: merge `getall` (Live) + `query` `c.status = @status` for **draft** and **pause**. Filters now hit POST `/api/Campaign/{tenant}` (`campaigns/session/query`), not getmany-by-ids.
- Duplicate: still `copyCampaign` (not save); POST body `{}`; slim normalized row; ignore double-clicks while pending.
- Unpublish: `lifecycleSavePayload` sends shell fields + `status: pause`, **no journey**.
- Conversations: `extractConversationIds` accepts `{ items: [{ conversationId }] }` and `{ Items: [{ ConversationId }] }`.
- `wrapApiEnvelope` also reads JSON `errors`.

Spec `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` left **Draft**. `docs/developer/journeys-ux.md` notes the list merge.

### Tests

`npm test` in `Journeys.UX`: **34 files, 174 passed** (was 167). Added: conversation camel/Pascal extract, duplicate→copy not save, unpublish payload omits journey / status pause (kebab test kept), authoring list merge, query allowlist, envelope `errors`.

No C# changes. Did not stop `Journeys.API`.

### Re-check (localhost:3000, signed in)

List now shows **untitled Draft** and **tier Live**. Duplicate of the untitled Draft still `copyCampaign` in ~35ms; running API `POST .../copy` is empty 404. Conversations GET `{"items":[]}` — **Resume agent** still hidden (parse would work if Items were present). Unpublish not re-clicked this pass (Live card restored; save payload is pause-without-journey).

### Remaining blockers

1. Restart `Journeys.API` so `POST .../copy` exists; empty 404 is the loaded binary, not UX.
2. Copy Fetch-by-id 404 vs query-found draft (untitled `82c3e8b3-…`) — Core/Backend point-read PK. Live `tiersystem` was fetchable earlier.
3. Resume: AgentMessage list is empty (`items: []`); conversation id from SSE is not in `ListThreads`.
4. Debug `agent-verify` still file-locks while API is running.

