### Task 5: New = Journey Builder + AgentChat rail

**Files:**
- Create (copy then remap): entire EXP `components/loyalty/campaign-journey-builder/**` except tests that import `@exp` without remap
- Create: `Journeys.UX/src/app/loyalty/campaigns/new/page.tsx`
- Modify: `use-coach-campaign-sync.ts` â†’ rename exports to unlabeled (`useAgentCampaignSync`); strip Coach strings
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

(Already passes after Task 4 â€” keep as regression. Add a store-level test once the store is lifted: dirty + snapshot does not call `hydrateFromCampaign`.)

- [ ] **Step 2: Copy builder trees from EXP**

Source: `temp/exp/apps/admin-web/src/components/loyalty/campaign-journey-builder/`.

Remap checklist (apply to **every** copied file):
- `@exp/shared-types` â†’ `@/lib/campaign-types` (extend that module with only imported types)
- `bffFetch` / `getSlug` / Prisma â†’ `journeysFetch` actions from Task 1
- Delete `CampaignTitleWithJsonInspector`; page title is text + `CampaignJsonDisclosure`
- Coach copy â†’ â€œAgentâ€
- `useCoachCampaignSync`: on discovered id, `getCampaign(id, "draft")`; if `hydrateDecision(store.isDirty) === "conflict"` set conflict UI (keep-local / apply-agent); else `hydrateFromCampaign`
- React Flow **only** for canvas components
- No JSON inspector dialog; no Coach chrome; no `CAMPAIGN_COACH_*`

`new/page.tsx`: session already from layout. Render builder shell + `<AgentChat />` in a rail. Pass `onDiscoveredCampaignId` into chat so the sync hook runs.

- [ ] **Step 3: `npm test` + `npx tsc --noEmit` in `Journeys.UX` â€” expect PASS.** Do not commit unless asked.

---

