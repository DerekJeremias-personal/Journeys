# Journeys.UX Campaigns IA Implementation Plan

> **Execution:** After approval, say execute and the intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not** use superpowers:subagent-driven-development. Linear unit issues land before any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Lift the remaining EXP Campaigns IA into `Journeys.UX` (list/kebab, New Journey Builder + agent rail, wizard, archive, versions), wire it HTTP-only to `Journeys.API`, and add Core copy/restore — with the shipped unlabeled `AgentChat` speaking the full SSE data plane and a collapsed Campaign JSON inspector.

**Architecture:** Surgical lift of EXP folder layout. `journeysFetch` allowlist expands to Campaign REST + conversations JSON. Copy/restore live in `CampaignService` (not a UX composite). `AgentChat` stays a thin transcript; stream body gains `linkedCampaignId` / `clientMessageId`; reducer parses `progress` / `mcp` / `workflow`. Neutral Tailwind/shadcn on the whole UX shell. No Campaign REST AdminAudit this increment.

**Tech Stack:** Next.js 16, React 19, NextAuth 5, Tailwind v4, shadcn/Radix primitives, TanStack Query, Vitest, React Flow (canvas only). `Journeys.API` / `Journeys.Core` net8.0.

**Spec:** `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- `Journeys.UX` must not project-reference Core, DAL, or Infra. HTTP to `Journeys.API` only.
- Never treat Auth0 `"hayward"` as `TenantId`. Use `resolveTenantId` (`JOURNEYS_TENANT_ID` wins).
- In-product name is **agent**. Do not ship “Campaign Coach”, `CAMPAIGN_COACH_*`, or other product names. Strings: “Agent”, “Open in Agent”, “Resume agent”, “Campaign JSON”.
- Unpublish is **Pause**, never Live→Draft on the same id. Duplicate calls **copy** API. Restore is a dedicated endpoint; Archive is immutable.
- Do not add `IAdminAuditService` to `CampaignController`. Keep `CampaignAgentToolAudit*`.
- Do not copy EXP `--color-brand-*`, `--font-brand`, purple sidebar tokens, charts, Prisma, `@exp/*`, PermissionGuard, or promotions CTA.
- Never `modelId` `"unknown"`. Campaigns omit client `modelId`.
- Never log API keys, JWT, prompts, full campaign JSON, or SSE bodies.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Construction after this plan is approved: fresh subagent per task. Do not skip Task 2 before Duplicate/Restore kebab. Do not treat New as done before Task 4 (hydrate).

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.UX/package.json` | Add Tailwind/shadcn/query/toast/lucide/radix; later zustand + reactflow |
| `Journeys.UX/postcss.config.mjs` | `@tailwindcss/postcss` |
| `Journeys.UX/src/app/globals.css` | Neutral Tailwind tokens (no EXP brand) + keep functional agent-chat rules until migrated |
| `Journeys.UX/src/lib/utils.ts` | `cn()` |
| `Journeys.UX/src/components/ui/*` | shadcn primitives used by campaigns trees |
| `Journeys.UX/src/components/providers.tsx` | QueryClientProvider + Toaster |
| `Journeys.UX/src/lib/map-loyalty-path.ts` | Campaign REST + conversations allowlist |
| `Journeys.UX/src/lib/journeys-fetch.ts` | Optional `searchParams` |
| `Journeys.UX/src/lib/api-types.ts` | Expand `Campaign` fields |
| `Journeys.UX/src/lib/campaign-types.ts` | Local Campaign/journey types for lifted files (not `@exp/shared-types`) |
| `Journeys.UX/src/lib/campaign-kebab.ts` | Pure kebab visibility + next status |
| `Journeys.UX/src/services/loyalty/actions.ts` | Campaign CRUD, copy, restore, PAT getall, conversations |
| `Journeys.UX/src/services/loyalty/query-keys.ts` | Lifted TanStack keys, no `@exp` |
| `Journeys.UX/src/components/loyalty/campaigns/*` | List, card, filters, archived, versions |
| `Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts` | Lift EXP helper |
| `Journeys.UX/src/lib/campaign-agent/chat-state.ts` | progressLabel, linkedCampaignId, discoveredCampaignId |
| `Journeys.UX/src/lib/campaign-agent/stream-route.ts` | Forward linkedCampaignId + clientMessageId |
| `Journeys.UX/src/components/loyalty/agent-chat.tsx` | Props + status line + inspector slot |
| `Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx` | Collapsed GET JSON |
| `Journeys.UX/src/components/loyalty/campaign-journey-builder/*` | New page canvas |
| `Journeys.UX/src/components/loyalty/campaigns/wizard/*` | Edit wizard |
| `Journeys.DTO/Requests/CopyCampaignRequest.cs` | Optional name |
| `Journeys.Core/Services/CampaignCopyFactory.cs` | Pure clone for copy vs restore |
| `Journeys.Core/Interfaces/Services/ICampaignService.cs` | `CopyCampaignAsync` / `RestoreArchivedCampaignAsync` |
| `Journeys.Core/Services/CampaignService.cs` | Fetch + factory + upsert |
| `Journeys.API/Controllers/CampaignController.cs` | `POST .../copy`, `POST .../restore` |
| `Journeys.Tests/Services/CampaignCopyFactoryTests.cs` | Clone rules |
| `Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs` | Service orchestration |
| `docs/developer/journeys-ux.md` | Routes + copy/restore + agent data plane |
| `docs/product/graph/path-map.yaml` | Add `journeys` to UX prefix |

Lift source root: `C:\Dev\Journeys\temp\exp\apps\admin-web\src`. Rewrite every `@exp/shared-types` import to `@/lib/campaign-types`. Rewrite `bffFetch` / `getSlug` to `journeysFetch` + tenant `"session"` slug (existing pattern). Strip `PermissionGuard` and EXP `PageWrapper` branding.

---

### Task 1: Neutral Tailwind + allowlist + campaign actions

**Files:**
- Modify: `Journeys.UX/package.json`
- Create: `Journeys.UX/postcss.config.mjs`
- Modify: `Journeys.UX/src/app/globals.css`
- Create: `Journeys.UX/src/lib/utils.ts`
- Create: `Journeys.UX/src/components/ui/button.tsx` (copy EXP, keep `cn`)
- Create: `Journeys.UX/src/components/ui/card.tsx`
- Create: `Journeys.UX/src/components/ui/alert.tsx`
- Create: `Journeys.UX/src/components/ui/alert-dialog.tsx`
- Create: `Journeys.UX/src/components/ui/dropdown-menu.tsx`
- Create: `Journeys.UX/src/components/ui/skeleton.tsx`
- Create: `Journeys.UX/src/components/ui/badge.tsx`
- Create: `Journeys.UX/src/components/providers.tsx`
- Modify: `Journeys.UX/src/app/layout.tsx`
- Modify: `Journeys.UX/src/app/loyalty/layout.tsx`
- Modify: `Journeys.UX/src/components/loyalty-nav.tsx`
- Modify: `Journeys.UX/src/lib/map-loyalty-path.ts`
- Modify: `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.test.ts` (if searchParams needs coverage)
- Modify: `Journeys.UX/src/lib/api-types.ts`
- Modify: `Journeys.UX/src/services/loyalty/actions.ts`

**Interfaces:**
- Consumes: existing `mapLoyaltyPath(path, tenantId)`, `journeysFetch(path, options)`
- Produces:
  - `mapLoyaltyPath` mappings in Step 3
  - `JourneysFetchOptions.searchParams?: Record<string, string | undefined>`
  - actions listed in Step 7

- [ ] **Step 1: Write failing allowlist tests**

Append to `Journeys.UX/src/lib/map-loyalty-path.test.ts` (keep existing cases). Match **literals before** `{id}`:

```ts
const t = "acme";

it("maps getmany, save, validate", () => {
  expect(mapLoyaltyPath("campaigns/session/getmany", t)).toBe("/api/Campaign/acme/getmany");
  expect(mapLoyaltyPath("campaigns/session/save", t)).toBe("/api/Campaign/acme/save");
  expect(mapLoyaltyPath("campaigns/session/validate", t)).toBe("/api/Campaign/acme/validate");
});

it("maps get-by-id, delete, copy, restore", () => {
  expect(mapLoyaltyPath("campaigns/session/cid-1", t)).toBe("/api/Campaign/acme/cid-1");
  expect(mapLoyaltyPath("campaigns/session/cid-1/copy", t)).toBe("/api/Campaign/acme/cid-1/copy");
  expect(mapLoyaltyPath("campaigns/session/cid-1/restore", t)).toBe("/api/Campaign/acme/cid-1/restore");
});

it("maps versions, archived, live/draft by ext, PAT getall", () => {
  expect(mapLoyaltyPath("campaigns/session/versions/ext-1", t)).toBe("/api/Campaign/acme/versions/ext-1");
  expect(mapLoyaltyPath("campaigns/session/archived", t)).toBe("/api/Campaign/acme/archived");
  expect(mapLoyaltyPath("campaigns/session/live/ext-1", t)).toBe("/api/Campaign/acme/live/ext-1");
  expect(mapLoyaltyPath("campaigns/session/draft/ext-1", t)).toBe("/api/Campaign/acme/draft/ext-1");
  expect(mapLoyaltyPath("campaigns/session/pointaccounttype/getall", t)).toBe(
    "/api/Campaign/acme/pointaccounttype/getall"
  );
});

it("maps campaign-agent conversations JSON", () => {
  expect(mapLoyaltyPath("campaign-agent/conversations", t)).toBe(
    "/api/v1/acme/campaign-agent/conversations"
  );
});

it("still rejects unknown paths", () => {
  expect(() => mapLoyaltyPath("campaigns/session/pointaccounttype/upsert", t)).toThrow(/not-allowlisted:/);
  expect(() => mapLoyaltyPath("campaigns/session/cid-1/stats", t)).toThrow(/not-allowlisted:/);
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `npm test` from `Journeys.UX` (or `npx vitest run src/lib/map-loyalty-path.test.ts`).

Expected: FAIL (`not-allowlisted` on `save` / `getmany` / copy).

- [ ] **Step 3: Implement `mapLoyaltyPath`**

Keep getall / schemas / events. Add, **in this order** (specific suffixes before generic `{id}`):

```ts
// after getall:
if (/^campaigns\/[^/]+\/getmany$/i.test(trimmed)) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/getmany`;
}
if (/^campaigns\/[^/]+\/save$/i.test(trimmed)) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/save`;
}
if (/^campaigns\/[^/]+\/validate$/i.test(trimmed)) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/validate`;
}
if (/^campaigns\/[^/]+\/archived$/i.test(trimmed)) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/archived`;
}
if (/^campaigns\/[^/]+\/pointaccounttype\/getall$/i.test(trimmed)) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/pointaccounttype/getall`;
}
const versions = /^campaigns\/[^/]+\/versions\/([^/]+)$/i.exec(trimmed);
if (versions) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/versions/${encodeURIComponent(versions[1])}`;
}
const live = /^campaigns\/[^/]+\/live\/([^/]+)$/i.exec(trimmed);
if (live) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/live/${encodeURIComponent(live[1])}`;
}
const draft = /^campaigns\/[^/]+\/draft\/([^/]+)$/i.exec(trimmed);
if (draft) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/draft/${encodeURIComponent(draft[1])}`;
}
const copy = /^campaigns\/[^/]+\/([^/]+)\/copy$/i.exec(trimmed);
if (copy) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(copy[1])}/copy`;
}
const restore = /^campaigns\/[^/]+\/([^/]+)\/restore$/i.exec(trimmed);
if (restore) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(restore[1])}/restore`;
}
const one = /^campaigns\/[^/]+\/([^/]+)$/i.exec(trimmed);
if (one && !/^(getall|getmany|save|validate|archived)$/i.test(one[1])) {
  return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(one[1])}`;
}
if (/^campaign-agent\/conversations$/i.test(trimmed)) {
  return `/api/v1/${encodeURIComponent(tenant)}/campaign-agent/conversations`;
}
```

Do **not** allowlist stats or PAT upsert.

- [ ] **Step 4: Re-run path tests — expect PASS**

- [ ] **Step 5: Add `searchParams` to `journeysFetch`**

In `JourneysFetchOptions` add `searchParams?: Record<string, string | undefined>`. After building `apiPath`, append encoded query (skip undefined/empty). Do not send `Cookie`. Do not add `X-Journeys-Audit` for Campaign REST.

- [ ] **Step 6: Neutral Tailwind + providers**

Add deps (do not add apex/charts/cmdk/prisma): `tailwindcss`, `@tailwindcss/postcss`, `@tanstack/react-query`, `lucide-react`, `class-variance-authority`, `clsx`, `tailwind-merge`, `radix-ui`, `react-hot-toast`.

`postcss.config.mjs`:

```js
const postcssConfig = { plugins: { "@tailwindcss/postcss": {} } };
export default postcssConfig;
```

`globals.css`: `@import "tailwindcss";` then a **neutral** `@theme` (oklch grays, system-ui). **Omit** `--color-brand-*`, `--font-brand`, EXP `--color-sidebar-*`. Keep existing `.agent-chat` / `.layout` rules until layouts use Tailwind.

Copy `cn` from EXP `src/lib/utils.ts` (`clsx` + `twMerge` only; you may keep `isMinDate`). Copy only the UI primitives listed in Files from EXP `components/ui/*`.

`providers.tsx` (`"use client"`): `QueryClientProvider` + `Toaster`. Wrap `{children}` in root `layout.tsx`.

Restyle `loyalty-nav` / loyalty layout with Tailwind equivalents of today’s gray 240px nav (`w-60 bg-zinc-100`). Do not use EXP purple sidebar.

- [ ] **Step 7: Campaign actions**

Expand `CampaignListItem` in `api-types.ts` (or alias `Campaign`) with `startDate?`, `endDate?`, `events?`, `journey?`. Add server actions in `actions.ts` using slug `"session"`:

| Function | Method | Path | Notes |
|----------|--------|------|--------|
| `getCampaigns` | POST | `campaigns/session/getall` | already exists |
| `getCampaignsByFilters` | POST | `campaigns/session/getmany` | body = filter request |
| `getCampaign(id, status?)` | GET | `campaigns/session/${id}` | `searchParams: { campaignStatus: status }` |
| `updateCampaign(data)` | POST | `campaigns/session/save` | body = campaign |
| `deleteCampaign(id, status)` | DELETE | `campaigns/session/${id}` | `searchParams: { status }` |
| `copyCampaign(id, status, name?)` | POST | `campaigns/session/${id}/copy` | `searchParams: { status }`, optional `{ name }` |
| `restoreCampaign(id)` | POST | `campaigns/session/${id}/restore` | `searchParams: { status: "archive" }` |
| `validateCampaign(data)` | POST | `campaigns/session/validate` | |
| `getCampaignVersions(ext)` | GET | `campaigns/session/versions/${ext}` | |
| `getArchivedCampaigns` | GET | `campaigns/session/archived` | |
| `getLiveByExt` / `getDraftByExt` | GET | live/draft paths | |
| `getPointAccountTypes` | POST | `campaigns/session/pointaccounttype/getall` | pageSize body like getall |
| `listAgentConversations` | GET | `campaign-agent/conversations` | parse `{ items: { conversationId }[] }` |

Reuse `extractEntities` / envelope. `copyCampaign` must **not** strip ids client-side.

- [ ] **Step 8: `npm test` in `Journeys.UX` — expect PASS.** Do not commit unless asked.

---

### Task 2: Core copy + restore

**Files:**
- Create: `Journeys.DTO/Requests/CopyCampaignRequest.cs`
- Create: `Journeys.Core/Services/CampaignCopyFactory.cs`
- Create: `Journeys.Tests/Services/CampaignCopyFactoryTests.cs`
- Modify: `Journeys.Core/Interfaces/Services/ICampaignService.cs`
- Modify: `Journeys.Core/Services/CampaignService.cs`
- Create: `Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs`
- Modify: `Journeys.API/Controllers/CampaignController.cs`

**Interfaces:**
- Consumes: `FetchCampaignAsync`, `UpsertCampaignAsync`, `GetDraftCampaignByExtIdAsync`
- Produces:
  - `CampaignCopyFactory.ForNewProgram(CampaignDto source, string? nameOverride): CampaignDto`
  - `CampaignCopyFactory.ForRestoreFromArchive(CampaignDto archive): CampaignDto`
  - `ICampaignService.CopyCampaignAsync(string tenantId, string campaignId, string status, string? name, CancellationToken cancellationToken = default): Task<CampaignDto>`
  - `ICampaignService.RestoreArchivedCampaignAsync(string tenantId, string campaignId, CancellationToken cancellationToken = default): Task<CampaignDto>`
  - `POST api/Campaign/{tenantId}/{campaignId}/copy?status=`
  - `POST api/Campaign/{tenantId}/{campaignId}/restore?status=archive` (status query required and must be archive)

- [ ] **Step 1: Failing factory tests**

`Journeys.Tests/Services/CampaignCopyFactoryTests.cs`:

```csharp
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignCopyFactoryTests
{
    private static CampaignDto Source() => new()
    {
        Id = "src-id",
        Etag = "etag",
        ExtCampaignId = "summer",
        Name = "Summer",
        Status = "live",
        StartDate = DateTimeOffset.Parse("2026-01-01Z"),
        Events = new List<string> { "evt" },
        DeployedDate = DateTimeOffset.Parse("2026-02-01Z"),
        ArchivedDate = DateTimeOffset.Parse("2026-03-01Z")
    };

    [Fact]
    public void ForNewProgram_new_id_clears_ext_draft_name_suffix()
    {
        var copy = CampaignCopyFactory.ForNewProgram(Source(), null);
        Assert.False(string.IsNullOrWhiteSpace(copy.Id));
        Assert.NotEqual("src-id", copy.Id);
        Assert.True(string.IsNullOrWhiteSpace(copy.ExtCampaignId));
        Assert.Equal("draft", copy.Status);
        Assert.Equal("Summer Copy", copy.Name);
        Assert.Null(copy.Etag);
        Assert.Null(copy.DeployedDate);
        Assert.Null(copy.ArchivedDate);
        Assert.Equal(new List<string> { "evt" }, copy.Events);
    }

    [Fact]
    public void ForNewProgram_uses_name_override()
    {
        var copy = CampaignCopyFactory.ForNewProgram(Source(), "Other");
        Assert.Equal("Other", copy.Name);
    }

    [Fact]
    public void ForRestoreFromArchive_new_id_same_ext_draft()
    {
        var archive = Source();
        archive.Status = "archive";
        var draft = CampaignCopyFactory.ForRestoreFromArchive(archive);
        Assert.NotEqual("src-id", draft.Id);
        Assert.Equal("summer", draft.ExtCampaignId);
        Assert.Equal("draft", draft.Status);
        Assert.Equal("Summer", draft.Name);
        Assert.Null(draft.Etag);
        Assert.Null(draft.DeployedDate);
        Assert.Null(draft.ArchivedDate);
    }
}
```

- [ ] **Step 2: `dotnet test` that class — expect FAIL** (type missing)

- [ ] **Step 3: Implement `CampaignCopyFactory`**

Deep-clone via `JsonSerializer` (same assembly `CampaignDto`) then apply field rules. New `Id` = `Guid.NewGuid().ToString()`. Copy must **not** keep `ExtCampaignId`. Restore **must** keep it. Status lowercase `draft`.

- [ ] **Step 4: Factory tests PASS**

- [ ] **Step 5: Service tests**

`CampaignServiceCopyRestoreTests`: recording `ICampaignAdapter` (throw `NotImplementedException` on unused members). `FetchCampaignAsync` returns a `Campaign` with id/status/name/ext. `UpsertCampaignAsync` captures the stored campaign and returns it. `GetDraftCampaignByExtIdAsync` returns null or a draft.

Cases:
1. `CopyCampaignAsync` fetches source, upserts clone with new id, empty ext, draft, name suffix.
2. `RestoreArchivedCampaignAsync` throws `APIErrorsException` when `GetDraftCampaignByExtIdAsync` returns a draft (`errors` key e.g. `extCampaignId`).
3. Restore when no draft: upsert new id, **same** ext, status draft. Adapter must not be asked to upsert status `archive`.

Wire `CampaignService` with `CampaignTestServices.CreateDefinitionValidator()` like `CampaignServiceDeletePatTests`. If definition validation blocks upsert, clone through the factory and have the service call `_campaignAdapter.UpsertCampaignAsync` after `CampaignShellValidator` only **if** that matches existing upsert (prefer calling `UpsertCampaignAsync` so definition rules stay). If the validator requires a journey, put a minimal valid journey on the source fixture using existing test factories.

- [ ] **Step 6: Implement service methods and interface**

```csharp
Task<CampaignDto> CopyCampaignAsync(string tenantId, string campaignId, string status, string? name = null, CancellationToken cancellationToken = default);
Task<CampaignDto> RestoreArchivedCampaignAsync(string tenantId, string campaignId, CancellationToken cancellationToken = default);
```

Copy: fetch source (null → throw/NotFound at controller). `CampaignCopyFactory.ForNewProgram` then `UpsertCampaignAsync`.

Restore: fetch with status `archive`. If null, not found. If `GetDraftCampaignByExtIdAsync` returns a document, throw `new APIErrorsException(new Dictionary<string, string> { ["extCampaignId"] = "A draft already exists for this program." })`. Else factory restore + upsert.

Do **not** call `IAdminAuditService`.

- [ ] **Step 7: Controller endpoints**

Follow `SaveCampaignAsync` style: validate tenant/id/status, call service, `APIErrorsException` → 400 `{ errors }`, unexpected → 500 safe message. `CopyCampaignRequest` body optional.

```csharp
public class CopyCampaignRequest
{
    public string? Name { get; set; }
}
```

Log `TenantId`, `campaignId`, action — not full JSON.

- [ ] **Step 8: Verify**

From `C:\Dev\Journeys\Journeys`:

```powershell
.\scripts\agent-verify.ps1 -Files @(
  "Journeys.Core/Services/CampaignCopyFactory.cs",
  "Journeys.Core/Services/CampaignService.cs",
  "Journeys.API/Controllers/CampaignController.cs",
  "Journeys.Tests/Services/CampaignCopyFactoryTests.cs",
  "Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs"
) -RunTests
```

Expected: `agent-verify: OK`. Do not commit unless asked.

---

### Task 3: List + kebab

**Files:**
- Create: `Journeys.UX/src/lib/campaign-kebab.ts`
- Create: `Journeys.UX/src/lib/campaign-kebab.test.ts`
- Create: `Journeys.UX/src/lib/campaign-types.ts` (minimal Campaign type for cards)
- Create: `Journeys.UX/src/components/loyalty/status-badge.tsx` (local; no `@exp/shared-types`)
- Create: `Journeys.UX/src/components/loyalty/campaigns/campaign-card.tsx`
- Create: `Journeys.UX/src/components/loyalty/campaigns/campaign-filters.tsx`
- Create: `Journeys.UX/src/components/loyalty/campaigns/campaigns-client.tsx`
- Create: `Journeys.UX/src/components/loyalty/campaigns/index.ts`
- Create: `Journeys.UX/src/services/loyalty/query-keys.ts`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/page.tsx`
- Add (if needed): `@testing-library/react`, `jsdom` — use `/** @vitest-environment jsdom */` only on component tests

**Interfaces:**
- Consumes: Task 1 actions (`updateCampaign`, `copyCampaign`, `deleteCampaign`)
- Produces:
  - `normalizeCampaignStatus(raw?: string): "live" | "draft" | "archive" | "pause" | string`
  - `campaignKebabVisibility(status: string, extCampaignId?: string): { edit, agent, duplicate, versions, publish, unpublish, archive, restore, delete }`
  - `kebabSaveStatus(action: "publish" | "unpublish" | "archive"): "live" | "pause" | "archive"`

- [ ] **Step 1: Failing kebab tests**

```ts
import { describe, expect, it } from "vitest";
import { campaignKebabVisibility, kebabSaveStatus, normalizeCampaignStatus } from "./campaign-kebab";

describe("normalizeCampaignStatus", () => {
  it("maps EXP leftovers", () => {
    expect(normalizeCampaignStatus("active")).toBe("live");
    expect(normalizeCampaignStatus("archived")).toBe("archive");
    expect(normalizeCampaignStatus("LIVE")).toBe("live");
    expect(normalizeCampaignStatus("pause")).toBe("pause");
  });
});

describe("campaignKebabVisibility", () => {
  it("Draft: edit, duplicate, publish, delete; no agent, unpublish, archive, restore", () => {
    const v = campaignKebabVisibility("draft", "ext");
    expect(v).toMatchObject({
      edit: true, agent: false, duplicate: true, versions: true,
      publish: true, unpublish: false, archive: false, restore: false, delete: true
    });
  });
  it("Live: agent, unpublish, archive; no delete, publish, restore", () => {
    const v = campaignKebabVisibility("live", "ext");
    expect(v).toMatchObject({
      edit: true, agent: true, publish: false, unpublish: true,
      archive: true, restore: false, delete: false
    });
  });
  it("Pause: publish, archive; no agent", () => {
    const v = campaignKebabVisibility("pause", "ext");
    expect(v.agent).toBe(false);
    expect(v.publish).toBe(true);
    expect(v.archive).toBe(true);
  });
  it("Archive: restore only among mutations; no delete/agent", () => {
    const v = campaignKebabVisibility("archive", "ext");
    expect(v).toMatchObject({
      agent: false, restore: true, delete: false, archive: false, publish: false
    });
  });
  it("hides versions without extCampaignId", () => {
    expect(campaignKebabVisibility("live", undefined).versions).toBe(false);
  });
});

describe("kebabSaveStatus", () => {
  it("does not map unpublish to draft", () => {
    expect(kebabSaveStatus("unpublish")).toBe("pause");
    expect(kebabSaveStatus("publish")).toBe("live");
    expect(kebabSaveStatus("archive")).toBe("archive");
  });
});
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement `campaign-kebab.ts`** to pass Step 1. Archive visibility = not archive and not draft (EXP `!archived && !draft`).

- [ ] **Step 4: Lift list UI**

Copy from EXP:

- `components/loyalty/campaigns/campaign-card.tsx`
- `campaign-filters.tsx`
- `campaigns-client.tsx`
- `index.ts`
- `services/loyalty/query-keys.ts` (campaigns keys only)

Remap:
- `@exp/shared-types` → `@/lib/campaign-types`
- `copyCampaign` action must call Task 1 `copyCampaign(id, status)` — **delete** EXP strip-and-save composite
- publish/unpublish/archive: `updateCampaign({ ...campaign, status: kebabSaveStatus(action) })` — never set `draft` on unpublish
- Agent item: `"Open in Agent"` link to `/loyalty/campaigns/${id}/agent?campaignStatus=` **only if** `visibility.agent`
- Publish: `AlertDialog` “Publish to Live?” before save
- Delete: Draft only + existing confirm
- Remove promotions CTA and `CampaignCoachResumeSection` (resume is Task 4)
- `StatusBadge`: local component; show `pause`
- Card title → `/loyalty/campaigns/${id}?campaignStatus=`
- New CTA → `/loyalty/campaigns/new` (route 404 until Task 5 is OK)
- `page.tsx`: client `CampaignsClient` (session already gated by loyalty layout). Keep the existing **Agent** link to `/loyalty/campaigns/agent`

On getall failure: show `.error` / destructive alert; **do not** render an empty list as success.

- [ ] **Step 5: `npm test` — expect PASS including kebab tests.** Do not commit unless asked.

---

### Task 4: Agent data plane + inspector + resume

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/chat-state.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/chat-state.test.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/stream-route.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/stream-route.test.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts`
- Create: `Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx`
- Modify: `Journeys.UX/src/components/loyalty/agent-chat.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/page.tsx` (resume link)
- Create: `Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx`

**Interfaces:**
- Consumes: shipped `applyAgentSseEvent`, `handleCampaignAgentStreamPost`
- Produces:
  - `extractCampaignIdFromMcpData(data: string): string | null`
  - `AgentChatState` adds `progressLabel: string | null`, `linkedCampaignId: string | null`, `discoveredCampaignId: string | null`
  - `hydrateDecision(isDirty: boolean): "apply" | "conflict"`
  - `AgentChat` props: `{ linkedCampaignId?: string | null; conversationId?: string | null }`
  - Stream JSON body includes `linkedCampaignId?`, `clientMessageId?`

- [ ] **Step 1: Failing tests**

`extract-campaign-id.ts` — lift EXP `findCampaignId` (uuid keys `id` / `campaignId` / `linkedCampaignId` and PascalCase). Test:

```ts
expect(extractCampaignIdFromMcpData(JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })))
  .toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
expect(extractCampaignIdFromMcpData("{}")).toBeNull();
```

Replace `chat-state` “ignores progress” with:

```ts
it("stores progress.label without dropping conversationId", () => {
  let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
  s = applyAgentSseEvent(s, { event: "progress", data: '{"label":"Thinking…"}' });
  expect(s.conversationId).toBe("c1");
  expect(s.progressLabel).toBe("Thinking…");
});

it("mcp/workflow set discoveredCampaignId from payload; unknown events leave state", () => {
  let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
  s = applyAgentSseEvent(s, {
    event: "mcp",
    data: JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })
  });
  expect(s.discoveredCampaignId).toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
  const before = { ...s };
  s = applyAgentSseEvent(s, { event: "nope", data: "{}" });
  expect(s.conversationId).toBe(before.conversationId);
});

it("error keeps conversationId and linkedCampaignId", () => {
  const start: AgentChatState = {
    conversationId: "c1",
    linkedCampaignId: "camp-1",
    discoveredCampaignId: "camp-1",
    progressLabel: "x",
    lines: [],
    streaming: true
  };
  const s = applyAgentSseEvent(start, { event: "error", data: '{"message":"nope"}' });
  expect(s.conversationId).toBe("c1");
  expect(s.linkedCampaignId).toBe("camp-1");
  expect(s.streaming).toBe(false);
});
```

`hydrate-policy.ts`:

```ts
export function hydrateDecision(isDirty: boolean): "apply" | "conflict" {
  return isDirty ? "conflict" : "apply";
}
```

`stream-route.test.ts`: when connect is mocked, forwarded JSON includes `linkedCampaignId` and `clientMessageId` if the request body had them.

- [ ] **Step 2: Run — expect FAIL** (progress currently ignored)

- [ ] **Step 3: Implement reducer, extract helper, stream forward**

`StreamBody`:

```ts
type StreamBody = {
  message?: string;
  conversationId?: string | null;
  linkedCampaignId?: string | null;
  clientMessageId?: string | null;
};
```

Forward those fields in `JSON.stringify` to upstream (omit nulls). Generate `clientMessageId` in `AgentChat` per send (`crypto.randomUUID()`).

On `progress`, set `progressLabel` from `label`. On `mcp` / `workflow` / `done` / `tool` / `tool_result`, set `discoveredCampaignId` when extract returns an id. Do **not** parse EXP `campaign_snapshot` as a required event (ignore if present).

- [ ] **Step 4: `AgentChat` + inspector + Live route + resume**

- Props: `linkedCampaignId`, `conversationId` (seed state; reload without query still new thread).
- Show `progressLabel` as one line above the transcript.
- After `discoveredCampaignId` or prop id: render `CampaignJsonDisclosure` — `<details>` **without** `open` (collapsed). Summary text `Campaign JSON`. Fetch via `getCampaign(id, "draft")` then without status. Pretty `JSON.stringify(data, null, 2)` + copy button. Errors stay inside the disclosure.
- Do not add JSON inspector modal, tool-activity log, or clear-session.
- `/loyalty/campaigns/[id]/agent/page.tsx`: read `id` + `campaignStatus`. If normalized status !== `live`, redirect to `/loyalty/campaigns/${id}?campaignStatus=` (or campaigns list) — do not render chat. If live, `<AgentChat linkedCampaignId={id} />`.
- List resume: `listAgentConversations()`; if `items[0].conversationId`, show “Resume agent” → `/loyalty/campaigns/agent?conversationId=`. Hide when empty.
- Agent page reads `searchParams.conversationId` and passes it into `AgentChat`.

Hydrate against the builder store is Task 5; this task only exports `hydrateDecision` and updates `discoveredCampaignId` so Task 5 can subscribe.

- [ ] **Step 5: `npm test` — expect PASS.** Do not commit unless asked.

---

### Task 5: New = Journey Builder + AgentChat rail

**Files:**
- Create (copy then remap): entire EXP `components/loyalty/campaign-journey-builder/**` except tests that import `@exp` without remap
- Create: `Journeys.UX/src/app/loyalty/campaigns/new/page.tsx`
- Modify: `use-coach-campaign-sync.ts` → rename exports to unlabeled (`useAgentCampaignSync`); strip Coach strings
- Modify: `use-save-campaign-draft.ts` / `use-create-empty-draft.ts` to `updateCampaign` / save (not EXP create composite)
- Add deps: `zustand`, `reactflow`, `immer` / `use-immer` **only if** copied files import them
- Modify: `package.json` accordingly

**Interfaces:**
- Consumes: `AgentChat`, `hydrateDecision`, `getCampaign`, `updateCampaign`, `validateCampaign`, `discoveredCampaignId` from chat state (callback `onDiscoveredCampaignId`)
- Produces: `/loyalty/campaigns/new` working builder + agent rail

- [ ] **Step 1: Add a failing hydrate-wiring test**

`Journeys.UX/src/components/loyalty/campaign-journey-builder/agent-hydrate.test.ts` (node):

```ts
import { hydrateDecision } from "@/lib/campaign-agent/hydrate-policy";
import { describe, expect, it } from "vitest";

describe("builder hydrate", () => {
  it("conflicts when dirty", () => {
    expect(hydrateDecision(true)).toBe("conflict");
    expect(hydrateDecision(false)).toBe("apply");
  });
});
```

(Already passes after Task 4 — keep as regression. Add a store-level test once the store is lifted: dirty + snapshot does not call `hydrateFromCampaign`.)

- [ ] **Step 2: Copy builder trees from EXP**

Source: `temp/exp/apps/admin-web/src/components/loyalty/campaign-journey-builder/`.

Remap checklist (apply to **every** copied file):
- `@exp/shared-types` → `@/lib/campaign-types` (extend that module with only imported types)
- `bffFetch` / `getSlug` / Prisma → `journeysFetch` actions from Task 1
- Delete `CampaignTitleWithJsonInspector`; page title is text + `CampaignJsonDisclosure`
- Coach copy → “Agent”
- `useCoachCampaignSync`: on discovered id, `getCampaign(id, "draft")`; if `hydrateDecision(store.isDirty) === "conflict"` set conflict UI (keep-local / apply-agent); else `hydrateFromCampaign`
- React Flow **only** for canvas components
- No JSON inspector dialog; no Coach chrome; no `CAMPAIGN_COACH_*`

`new/page.tsx`: session already from layout. Render builder shell + `<AgentChat />` in a rail. Pass `onDiscoveredCampaignId` into chat so the sync hook runs.

- [ ] **Step 3: `npm test` + `npx tsc --noEmit` in `Journeys.UX` — expect PASS.** Do not commit unless asked.

---

### Task 6: Wizard edit

**Files:**
- Create (copy then remap): EXP `components/loyalty/campaigns/wizard/**`
- Create: `Journeys.UX/src/app/loyalty/campaigns/[id]/page.tsx`
- Wizard already imports `getPointAccountTypes` / `getAllSchemas` / `updateCampaign`

**Interfaces:**
- Consumes: `getCampaign`, `updateCampaign`, `validateCampaign`, `getPointAccountTypes`, `getAllSchemas`
- Produces: `/loyalty/campaigns/[id]?campaignStatus=` wizard

- [ ] **Step 1: Failing Live-edit policy test**

`Journeys.UX/src/lib/campaign-live-edit.ts`:

```ts
export async function resolveWizardCampaign(args: {
  requested: { id: string; status: string };
  getCampaign: (id: string, status?: string) => Promise<{ success: boolean; data?: { id?: string; extCampaignId?: string; status?: string } }>;
  getDraftByExt: (ext: string) => Promise<{ success: boolean; data?: { id?: string } | null }>;
}): Promise<{ mode: "edit"; id: string; status: string } | { mode: "new-draft-same-ext"; extCampaignId: string }> {
  const status = args.requested.status.toLowerCase();
  if (status !== "live") {
    return { mode: "edit", id: args.requested.id, status };
  }
  const live = await args.getCampaign(args.requested.id, "live");
  const ext = live.data?.extCampaignId;
  if (!ext) return { mode: "edit", id: args.requested.id, status: "live" };
  const draft = await args.getDraftByExt(ext);
  if (draft.success && draft.data?.id) {
    return { mode: "edit", id: draft.data.id, status: "draft" };
  }
  return { mode: "new-draft-same-ext", extCampaignId: ext };
}
```

Test: live with existing draft → edit that draft id; live with no draft → `new-draft-same-ext`; draft request → edit as draft.

- [ ] **Step 2: Run — expect FAIL then implement the helper**

- [ ] **Step 3: Lift wizard**

Remap `@exp/shared-types`, PAT/schema actions, no PermissionGuard. Save Live journey in place is forbidden: if `resolveWizardCampaign` is `new-draft-same-ext`, save must POST a **new** id (omit `id`) with **same** `extCampaignId` and `status: draft`. If `edit` draft, save that draft id.

`[id]/page.tsx`: require `campaignStatus` query; fetch; render wizard. Missing campaign → error + link back to list.

- [ ] **Step 4: `npm test` + `tsc --noEmit` — expect PASS.** Do not commit unless asked.

---

### Task 7: Archive + versions

**Files:**
- Create: `Journeys.UX/src/components/loyalty/campaigns/archived-campaigns-client.tsx`
- Create: `Journeys.UX/src/components/loyalty/campaigns/campaign-versions-client.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/archived/page.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/versions/[extCampaignId]/page.tsx`
- Modify: list kebab Versions → `/loyalty/campaigns/versions/${extCampaignId}`; Archived nav/button on list

**Interfaces:**
- Consumes: `getArchivedCampaigns`, `restoreCampaign`, `getCampaignVersions`
- Produces: those two routes

- [ ] **Step 1: Failing restore-action test** (pure)

`Journeys.UX/src/lib/campaign-restore-action.test.ts`:

```ts
import { describe, expect, it, vi } from "vitest";
import { restoreArchived } from "./campaign-restore-action";

it("calls restore endpoint not save-as-draft", async () => {
  const restoreCampaign = vi.fn(async () => ({ success: true, timestamp: "" }));
  const updateCampaign = vi.fn();
  await restoreArchived({ id: "a1", restoreCampaign, updateCampaign });
  expect(restoreCampaign).toHaveBeenCalledWith("a1");
  expect(updateCampaign).not.toHaveBeenCalled();
});
```

```ts
export async function restoreArchived(args: {
  id: string;
  restoreCampaign: (id: string) => Promise<{ success: boolean; error?: string }>;
  updateCampaign: (c: unknown) => Promise<unknown>;
}) {
  return args.restoreCampaign(args.id);
}
```

- [ ] **Step 2: FAIL then implement helper**

- [ ] **Step 3: Lift EXP archived + versions clients**

Remap restore to `restoreCampaign` (**not** `updateCampaign({ status: "draft" })`). Toast API errors. List page links to `/loyalty/campaigns/archived`.

- [ ] **Step 4: `npm test` — expect PASS.** Do not commit unless asked.

---

### Task 8: Docs, graph, browser pass

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/campaign-agent-llm.md` (one line: UX rail hydrates from SSE; provider unchanged)
- Modify: `docs/product/graph/path-map.yaml` — `Journeys.UX` nodes: `[campaigns, journeys, campaign-agent]`
- Modify: `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` status → Approved after implementation (human still merges)
- Modify: `docs/platform/architecture.md` only if docs-impact requires a sentence that UX now mutates campaigns over HTTP (UX is still not write authority)

**Interfaces:**
- Consumes: shipped behavior from Tasks 1–7
- Produces: docs-impact / graph-impact clean on the implementation file set

- [ ] **Step 1: Update `journeys-ux.md`**

Document: campaigns are mutable; routes in spec §6; copy/restore; Unpublish=Pause; unlabeled agent data plane; collapsed Campaign JSON; no Campaign AdminAudit this increment; Tailwind is Journeys-neutral.

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

1. Sign in → Campaigns cards/filters.
2. Kebab: Publish confirm; Unpublish results in **pause**; Duplicate hits copy (new id + new ext); Delete Draft-only.
3. New: builder + agent rail; one SSE turn; builder hydrates or conflict; inspector collapsed then expands GET JSON.
4. Wizard edit; Live edit does not upsert Live journey in place.
5. Archive list restore; versions page.
6. Live-only `/[id]/agent`; non-live redirects.
7. Resume link when conversations exist.

Do not commit unless asked.

---

## Spec coverage (self-review)

| Spec | Task |
|------|------|
| G1 routes | 3, 4, 5, 6, 7 |
| G2 New builder + agent | 5 (after 4) |
| G3 kebab + confirm + Live-only agent | 3, 4 |
| G4 wizard edit | 6 |
| G5 HTTP-only allowlist | 1 |
| G6 copy Core | 2, 3 Duplicate |
| G7 SSE data plane + inspector | 4, 5 |
| G8 tenant / no unknown modelId | 1 (existing resolveTenantId) |
| G9 neutral Tailwind, EXP folders | 1, 3, 5, 6 |
| Restore API + archive UI | 2, 7 |
| Pause unpublish | 3 `kebabSaveStatus` |
| No AdminAudit | 2 explicit |
| No Coach copy | 3–5 remap |
| Docs/graph | 8 |
| Non-goals (promotions, PAT upsert, stats, Playwright, MCP copy tool) | not scheduled |
