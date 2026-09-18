# Task 6 report: Wizard edit

## Status

**DONE_WITH_CONCERNS**

## What I implemented

- TDD helper `resolveWizardCampaign` in `src/lib/campaign-live-edit.ts` using the brief signature: Live with an existing Draft → edit that Draft id; Live with no Draft → `new-draft-same-ext`; non-Live → edit as requested.
- Wizard save identity: `new-draft-same-ext` POSTs via `updateCampaign` with **omitted `id`**, **same `extCampaignId`**, and `status: draft`. Edit mode saves the Draft (or requested) id. There is no `createCampaign` action.
- Lifted EXP `components/loyalty/campaigns/wizard/**` into Journeys.UX, remapped `@exp/shared-types` → `@/lib/campaign-types`, stripped PermissionGuard / PageWrapper / ELP copy, and wired PAT/schema/save to existing session actions.
- Added `/loyalty/campaigns/[id]/page.tsx`: `campaignStatus` required; fetch via `resolveWizardCampaign`; missing campaign → error + link back to list. Does not collide with `[id]/agent`.
- Copied missing shadcn primitives and npm deps the wizard actually imports (`react-hook-form`, `@hookform/resolvers`, `reactflow`, `cmdk`). Did not add Immer/`@exp/*`/sonner.
- Lifted `loyalty-rule-builder` plus product-selector / taxonomy-picker so outcome/navigation/promotion **editor modals** compile. Did **not** lift promotions inventory/list marketing pages.

## TDD Evidence

**RED** (`npm test -- src/lib/campaign-live-edit.test.ts`):

```
FAIL  src/lib/campaign-live-edit.test.ts
Error: Cannot find module './campaign-live-edit'
Test Files  1 failed (1)
Tests  no tests
```

**GREEN** (same command after implementing the helper):

```
✓ src/lib/campaign-live-edit.test.ts (3 tests)
Test Files  1 passed (1)
Tests  3 passed (3)
```

Cases: live + existing draft → `{ mode: "edit", id: draftId, status: "draft" }`; live + no draft → `{ mode: "new-draft-same-ext", extCampaignId }`; draft request → edit as draft without calling get/live-ext.

## What I tested

- `npm test`: PASS — 23 files, 125 tests (includes the 3 Live-edit cases and lifted rule-builder parse/build tests).
- `npx tsc --noEmit`: only the allowed pre-existing errors remain:
  - `src/auth.ts(47,40)`
  - `src/lib/loyalty-model.test.ts(40,12)`
- `docs-impact.ps1`: PASS
- `graph-impact.ps1`: PASS (`campaigns`, `campaign-agent`, waiver)
- Browser (signed-in localhost:3000):
  - Missing `campaignStatus` → error + Back to campaigns
  - Unknown id + `campaignStatus=draft` → error + Back to campaigns
  - Live card `testtenant1 tier system` → `/loyalty/campaigns/{id}?campaignStatus=live` wizard, copy says new Draft same `extCampaignId`, status Draft, Delete hidden
  - Details → Journey (React Flow graph) → node Promotions panel → Edit promotion modal (rule builder) → Review with **Save changes** (did not submit, to avoid creating a Draft)

`/loyalty/campaigns/new` was not replaced.

## Files changed (paths + purpose)

| Path | Purpose |
|------|---------|
| `src/lib/campaign-live-edit.ts` | Live-edit policy + save identity helper |
| `src/lib/campaign-live-edit.test.ts` | Failing-then-passing policy tests |
| `src/lib/campaign-types.ts` | Wizard schemas/types; re-exports rule/taxonomy types |
| `src/lib/campaign-rule-types.ts` | Lifted rules-engine Zod unions (LoyaltyRule, Outcome, Navigation) |
| `src/lib/campaign-taxonomy-types.ts` | TaxonomyDto for the picker |
| `src/app/loyalty/campaigns/[id]/page.tsx` | Wizard edit route |
| `src/components/loyalty/campaigns/wizard/**` | Lifted EXP wizard tree |
| `src/components/loyalty/promotions/loyalty-rule-builder/**` | Rule builder used by wizard modals |
| `src/components/loyalty/promotions/index.ts` | Barrel: rule builder only (no promotions marketing CTA) |
| `src/components/loyalty/product-selector/**` | Transitive dep of taxonomic conditions |
| `src/components/loyalty/taxonomy-picker/**` | Transitive dep of product selector |
| `src/components/ui/{tabs,command,form,input,popover,select,dialog,label,textarea,toggle-group,switch,toggle,checkbox,scroll-area}.tsx` | Missing shadcn primitives |
| `src/components/shared/json-tree-view.tsx` | Review JSON preview without `react-json-view-lite` |
| `src/services/loyalty/actions.ts` | `updateCampaign` accepts `Campaign` |
| `src/services/loyalty/query-keys.ts` | schemas / PAT / dashboard keys the wizard invalidates |
| `src/services/loyalty/taxonomy-actions.ts` | Empty-success stubs (taxonomy HTTP is not allowlisted) |
| `src/components/loyalty/campaigns/campaign-card.tsx` | null-safe `extCampaignId` |
| `src/components/loyalty/campaigns/campaigns-client.tsx` | null-safe `extCampaignId` |
| `package.json` / `package-lock.json` | `react-hook-form`, `@hookform/resolvers`, `reactflow`, `cmdk` |
| `docs/developer/journeys-ux.md` | Wizard edit route + Live-edit save rules |
| `docs/product/graph/waivers/2026-09-18-campaigns-wizard-edit.md` | No new capability |

## Self-review

- Save Live in place is forbidden for the no-draft path: page strips `id`, forces `status: draft`, keeps `extCampaignId`, `saveMode="new-draft-same-ext"`.
- Existing Draft for the same ext is fetched and edited by Draft id (`saveMode="edit"`).
- Wizard still uses EXP “Promotions” as **journey RuleSets** (node editor), not a promotions marketing CTA.
- `createCampaign` action import removed; templates’ `createCampaign()` methods are local factory names only.
- `[id]/agent` still redirects non-Live into this wizard with `campaignStatus`.

## Concerns

1. **Taxonomy HTTP is not allowlisted.** `taxonomy-actions.ts` returns empty lists so the picker compiles. Product-based (taxonomic) conditions cannot browse catalogs until a later task adds those API paths.
2. **Types split.** Rule/taxonomy Zod lives in sibling files re-exported from `campaign-types.ts` so the wizard import path stays `@/lib/campaign-types`. The plan said extend that file, not split; this avoided a single 1k-line types dump.
3. **Brief fallback:** if Live has no `extCampaignId`, `resolveWizardCampaign` returns `edit` on the Live id (verbatim helper). Product ontology still forbids Live journey upsert; that path is empty-ext only.
4. **Did not click Save** on the live tenant campaign, so new-draft persist was not exercised end-to-end against the API.
5. Details step showed unmatched event GUIDs vs eventable schema names on this tenant’s Live campaign (data/schema matching, not a compile failure).

## Git

No commits, pushes, merges, or PR changes were made. Nothing was staged.

---

# Task 6 review-fix report

## Status

**DONE**

## What I fixed

1. **Pinned Live `extCampaignId` on `new-draft-same-ext`.** `applyWizardSaveIdentity` now takes `{ extCampaignId?, sourceStatus? }` and always overwrites `extCampaignId` from the resolved/initial pin — not the form. Details step locks the field and skips slugify. Wizard submit passes `initial.extCampaignId`, so a mutated form ext cannot leak.
2. **Unit tests for `applyWizardSaveIdentity`** in `src/lib/campaign-live-edit.test.ts`: new-draft omits `id`, forces `draft`, pins ext over a mutated form value, strips `etag`; edit leaves `id`.
3. **Empty-ext Live cannot POST Live→Draft on the same id.** Live same-id edit locks status to `live`/`pause` in the details select. Helper coerces any other status (including `draft`/`archive`) back to `live`; `pause` is allowed. Unpublish remains Pause.
4. **New-draft strips Live `etag`.** Page clears `etag` on the initial Campaign. Helper `delete`s `etag` (and `id`) for `new-draft-same-ext`.

TDD: wrote failing `applyWizardSaveIdentity` tests first (3 failed: pinned ext received the options object; Live→draft stayed `draft`; archive stayed `archive`), then implemented the helper.

## Tests

**`npm test -- src/lib/campaign-live-edit.test.ts`** — PASS

```
✓ src/lib/campaign-live-edit.test.ts (7 tests)
Test Files  1 passed (1)
Tests  7 passed (7)
```

RED (before helper change): 3 failed | 4 passed (7). GREEN: 7 passed (7).

**`npm test`** — PASS

```
Test Files  23 passed (23)
Tests  129 passed (129)
```

## Git

No commits, pushes, merges, or PRs.
