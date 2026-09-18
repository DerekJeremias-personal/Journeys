### Task 7: Archive + versions

**Files:**
- Create: `Journeys.UX/src/components/loyalty/campaigns/archived-campaigns-client.tsx`
- Create: `Journeys.UX/src/components/loyalty/campaigns/campaign-versions-client.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/archived/page.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/versions/[extCampaignId]/page.tsx`
- Modify: list kebab Versions â†’ `/loyalty/campaigns/versions/${extCampaignId}`; Archived nav/button on list

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

- [ ] **Step 4: `npm test` â€” expect PASS.** Do not commit unless asked.

---

