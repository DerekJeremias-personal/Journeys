### Task 6: Loyalty actions + Campaigns + Accounts pages

**Files:**
- Create: `Journeys.UX/src/services/loyalty/actions.ts`
- Create: `Journeys.UX/src/app/loyalty/campaigns/page.tsx`
- Create: `Journeys.UX/src/app/loyalty/accounts/page.tsx`
- Test: `Journeys.UX/src/services/loyalty/parse-list.test.ts`

**Interfaces:**
- Consumes: `journeysFetch`
- Produces:
  - `getCampaigns(): Promise<ApiResponse<CampaignListItem[]>>` — `journeysFetch("campaigns/{tenant}/getall", { method: "POST", body: { pageSize: 100, continuationToken: null } })` then `extractEntities`
  - `getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>>` — POST `schemas/{tenant}/model/all` with body `{}` then normalize array or `{ items|Items: [] }`
  - `getSchemaByName(name: string)` — `getAllSchemas()`, case-insensitive name match AND `status` case-insensitive `"live"`
  - `queryData(schemaName: string): Promise<ApiResponse<Record<string, unknown>[]>>` — POST `events/{tenant}/{schemaName}/admin/query` with body `{ query: "", parameters: {}, pageSize: 50, continuationToken: null, sortBy: "", sortOrder: "ASC" }` then `extractEntities`
- `extractEntities(data: unknown): unknown[]` — if `Array.isArray(data)` return it; if `data.entities` or `data.Entities` array return that; if `data.items` or `data.Items` array return that; else `[]`

Constant: `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME = "LoyaltyAccountDetails"`

- [ ] **Step 1: Write `parse-list.test.ts` for `extractEntities` and `pickLiveSchema(schemas, name)`** (implement those two functions in `src/services/loyalty/parse-list.ts` — keep `actions.ts` as `"use server"` wrappers only).

```ts
import { describe, expect, it } from "vitest";
import { extractEntities, pickLiveSchema } from "./parse-list";

describe("extractEntities", () => {
  it("returns arrays as-is", () => {
    expect(extractEntities([{ a: 1 }])).toEqual([{ a: 1 }]);
  });
  it("reads entities", () => {
    expect(extractEntities({ entities: [{ id: "1" }] })).toEqual([{ id: "1" }]);
  });
  it("reads Items", () => {
    expect(extractEntities({ Items: [{ id: "2" }] })).toEqual([{ id: "2" }]);
  });
});

describe("pickLiveSchema", () => {
  it("matches live name case-insensitively", () => {
    const s = pickLiveSchema(
      [
        { name: "LoyaltyAccountDetails", status: "Live" },
        { name: "LoyaltyAccountDetails", status: "Draft" }
      ],
      "loyaltyaccountdetails"
    );
    expect(s?.status).toBe("Live");
  });
  it("returns null when missing", () => {
    expect(pickLiveSchema([], "LoyaltyAccountDetails")).toBeNull();
  });
});
```

- [ ] **Step 2: Implement `parse-list.ts` so tests PASS.** `pickLiveSchema` returns the first item where `name` equals (case-insensitive) and `status === "Live"` (case-insensitive).

- [ ] **Step 3: Implement `"use server"` `actions.ts` calling `journeysFetch` + parse helpers.** Use path templates with a dummy slug `session` — `mapLoyaltyPath` ignores the slug. Example path string: `campaigns/session/getall`.

- [ ] **Step 4: Campaigns page (server component)**  
  Call `getCampaigns()`. On `!success` show `<p className="error">` with `error` (or “not authorized / check tenant or key” when error looks like 401/403 / nope). On empty array show `<p className="empty">No campaigns found.</p>`. Else a `<ul>` of `name` — `status` — `id`. No buttons.

- [ ] **Step 5: Accounts page (server component)**  
  `getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`. If null: `<p className="empty">The LoyaltyAccountDetails schema is missing or not Live for this tenant.</p>` — **no** builder link. If schema ok, `queryData(schema.name ?? LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`. Render `<table>`: column headers from `schema.attributes` `displayName ?? name`, cells from row `[attr.name]`. If attributes missing/empty, columns `id` and `name` only. Empty rows: `No loyalty accounts found.`

- [ ] **Step 6: `npm test` PASS. `npx tsc --noEmit` PASS.**

- [ ] **Step 7: Do not commit** unless the user asks in that message.
