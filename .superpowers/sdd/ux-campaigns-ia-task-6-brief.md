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

Test: live with existing draft â†’ edit that draft id; live with no draft â†’ `new-draft-same-ext`; draft request â†’ edit as draft.

- [ ] **Step 2: Run â€” expect FAIL then implement the helper**

- [ ] **Step 3: Lift wizard**

Remap `@exp/shared-types`, PAT/schema actions, no PermissionGuard. Save Live journey in place is forbidden: if `resolveWizardCampaign` is `new-draft-same-ext`, save must POST a **new** id (omit `id`) with **same** `extCampaignId` and `status: draft`. If `edit` draft, save that draft id.

`[id]/page.tsx`: require `campaignStatus` query; fetch; render wizard. Missing campaign â†’ error + link back to list.

- [ ] **Step 4: `npm test` + `tsc --noEmit` â€” expect PASS.** Do not commit unless asked.

---

