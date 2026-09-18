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
- Add (if needed): `@testing-library/react`, `jsdom` â€” use `/** @vitest-environment jsdom */` only on component tests

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

- [ ] **Step 2: Run â€” expect FAIL**

- [ ] **Step 3: Implement `campaign-kebab.ts`** to pass Step 1. Archive visibility = not archive and not draft (EXP `!archived && !draft`).

- [ ] **Step 4: Lift list UI**

Copy from EXP:

- `components/loyalty/campaigns/campaign-card.tsx`
- `campaign-filters.tsx`
- `campaigns-client.tsx`
- `index.ts`
- `services/loyalty/query-keys.ts` (campaigns keys only)

Remap:
- `@exp/shared-types` â†’ `@/lib/campaign-types`
- `copyCampaign` action must call Task 1 `copyCampaign(id, status)` â€” **delete** EXP strip-and-save composite
- publish/unpublish/archive: `updateCampaign({ ...campaign, status: kebabSaveStatus(action) })` â€” never set `draft` on unpublish
- Agent item: `"Open in Agent"` link to `/loyalty/campaigns/${id}/agent?campaignStatus=` **only if** `visibility.agent`
- Publish: `AlertDialog` â€œPublish to Live?â€ before save
- Delete: Draft only + existing confirm
- Remove promotions CTA and `CampaignCoachResumeSection` (resume is Task 4)
- `StatusBadge`: local component; show `pause`
- Card title â†’ `/loyalty/campaigns/${id}?campaignStatus=`
- New CTA â†’ `/loyalty/campaigns/new` (route 404 until Task 5 is OK)
- `page.tsx`: client `CampaignsClient` (session already gated by loyalty layout). Keep the existing **Agent** link to `/loyalty/campaigns/agent`

On getall failure: show `.error` / destructive alert; **do not** render an empty list as success.

- [ ] **Step 5: `npm test` â€” expect PASS including kebab tests.** Do not commit unless asked.

---

