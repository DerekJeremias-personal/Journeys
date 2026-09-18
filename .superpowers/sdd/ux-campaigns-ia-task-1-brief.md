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

- [ ] **Step 2: Run tests â€” expect FAIL**

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

- [ ] **Step 4: Re-run path tests â€” expect PASS**

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

Restyle `loyalty-nav` / loyalty layout with Tailwind equivalents of todayâ€™s gray 240px nav (`w-60 bg-zinc-100`). Do not use EXP purple sidebar.

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

- [ ] **Step 8: `npm test` in `Journeys.UX` â€” expect PASS.** Do not commit unless asked.

---

