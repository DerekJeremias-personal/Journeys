### Task 1: Allowlist + audit + `queryData` params

**Files:**
- Modify: `Journeys.UX/src/lib/map-loyalty-path.ts`
- Modify: `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.test.ts`
- Modify: `Journeys.UX/src/lib/api-types.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.test.ts`
- Modify: `Journeys.UX/src/services/loyalty/actions.ts`

**Interfaces:**
- Consumes: existing `mapLoyaltyPath(path, tenantId)`, `journeysFetch(path, options, deps?)`
- Produces:
  - Account/Journey maps in Step 3
  - `AdminAuditHeader` + `JourneysFetchOptions.audit`
  - `QueryDataParams` object `queryData`
  - `extractContinuationToken(data: unknown): string | null`

- [ ] **Step 1: Write failing allowlist tests**

Append to `Journeys.UX/src/lib/map-loyalty-path.test.ts` (keep existing `t`):

```ts
it("maps account get, ext, balances, ledgers, deposit, withdrawal", () => {
  expect(mapLoyaltyPath("accounts/session/acc-1", t)).toBe(`/api/Account/${t}/acc-1`);
  expect(mapLoyaltyPath("accounts/session/ext/xref-1", t)).toBe(`/api/Account/${t}/ext/xref-1`);
  expect(mapLoyaltyPath("accounts/session/points/balances/acc-1", t)).toBe(
    `/api/Account/${t}/points/balances/acc-1`
  );
  expect(mapLoyaltyPath("accounts/session/points/acc-1", t)).toBe(`/api/Account/${t}/points/acc-1`);
  expect(mapLoyaltyPath("accounts/session/points/deposit", t)).toBe(`/api/Account/${t}/points/deposit`);
  expect(mapLoyaltyPath("accounts/session/points/withdrawal", t)).toBe(
    `/api/Account/${t}/points/withdrawal`
  );
});

it("does not treat points/deposit as an account id", () => {
  expect(mapLoyaltyPath("accounts/session/points/deposit", t)).not.toBe(`/api/Account/${t}/points`);
});

it("maps journey enter, exit, preview, move", () => {
  expect(
    mapLoyaltyPath(
      "journey/session/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1",
      t
    )
  ).toBe(`/api/Journey/${t}/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1`);
  expect(
    mapLoyaltyPath(
      "journey/session/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1",
      t
    )
  ).toBe(`/api/Journey/${t}/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1`);
  expect(mapLoyaltyPath("journey/session/PreviewTierMove", t)).toBe(
    `/api/Journey/${t}/PreviewTierMove`
  );
  expect(mapLoyaltyPath("journey/session/MoveTier", t)).toBe(`/api/Journey/${t}/MoveTier`);
});

it("rejects account builder, expire path, and points admin", () => {
  expect(() => mapLoyaltyPath("accounts/session/builder", t)).toThrow(/not-allowlisted:/);
  expect(() => mapLoyaltyPath("accounts/session/points/expire", t)).toThrow(/not-allowlisted:/);
  expect(() => mapLoyaltyPath("accounts/session/points/admin", t)).toThrow(/not-allowlisted:/);
});
```

- [ ] **Step 2: Run tests — expect FAIL** on the new cases.

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/map-loyalty-path.test.ts
```

- [ ] **Step 3: Implement maps** in `map-loyalty-path.ts` **before** the final `throw`. Specific account paths first. Encode every captured segment.

```ts
  const enc = (s: string) => encodeURIComponent(s);

  if (/^accounts\/[^/]+\/points\/deposit$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/deposit`;
  }
  if (/^accounts\/[^/]+\/points\/withdrawal$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/withdrawal`;
  }
  const balances = /^accounts\/[^/]+\/points\/balances\/([^/]+)$/i.exec(trimmed);
  if (balances) {
    return `/api/Account/${enc(tenant)}/points/balances/${enc(balances[1])}`;
  }
  const ledgers = /^accounts\/[^/]+\/points\/([^/]+)$/i.exec(trimmed);
  if (ledgers && !/^(deposit|withdrawal|balances|admin|expire)$/i.test(ledgers[1])) {
    return `/api/Account/${enc(tenant)}/points/${enc(ledgers[1])}`;
  }
  const ext = /^accounts\/[^/]+\/ext\/([^/]+)$/i.exec(trimmed);
  if (ext) {
    return `/api/Account/${enc(tenant)}/ext/${enc(ext[1])}`;
  }
  const accountOne = /^accounts\/[^/]+\/([^/]+)$/i.exec(trimmed);
  if (accountOne && !/^(points|ext|builder)$/i.test(accountOne[1])) {
    return `/api/Account/${enc(tenant)}/${enc(accountOne[1])}`;
  }

  const enter =
    /^journey\/[^/]+\/ManuallyEnter\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (enter) {
    return `/api/Journey/${enc(tenant)}/ManuallyEnter/${enc(enter[1])}/Journey/${enc(enter[2])}/ForAccount/${enc(enter[3])}`;
  }
  const exit =
    /^journey\/[^/]+\/ManuallyExit\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (exit) {
    return `/api/Journey/${enc(tenant)}/ManuallyExit/${enc(exit[1])}/Journey/${enc(exit[2])}/ForAccount/${enc(exit[3])}`;
  }
  if (/^journey\/[^/]+\/PreviewTierMove$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/PreviewTierMove`;
  }
  if (/^journey\/[^/]+\/MoveTier$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/MoveTier`;
  }
```

- [ ] **Step 4: Re-run allowlist tests — expect PASS.**

- [ ] **Step 5: Write failing audit + continuation tests**

Append to `journeys-fetch.test.ts` (reuse existing `session()` helper; do not add named-tenant env stubs):

```ts
  it("attaches X-Journeys-Audit on audit option and uses session user id", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () => new Response("{}", { status: 200 }));
    await journeysFetch(
      "accounts/session/points/deposit",
      {
        method: "POST",
        body: { amount: 1 },
        audit: {
          loyaltyMemberId: "acc-1",
          actionType: "Point Adjustment",
          action: "Deposit",
          comment: "ops"
        }
      },
      {
        getSession: async () => session(),
        fetch: fetchMock as unknown as typeof fetch,
        apiBaseUrl: "https://api.example"
      }
    );
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    const header = (init.headers as Record<string, string>)["X-Journeys-Audit"];
    expect(header).toBeTruthy();
    const parsed = JSON.parse(header) as { adminUserId: string; action: string; loyaltyMemberId: string };
    expect(parsed.adminUserId).toBe("u1");
    expect(parsed.action).toBe("Deposit");
    expect(parsed.loyaltyMemberId).toBe("acc-1");
  });

  it("does not attach X-Journeys-Audit on GET balances", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("accounts/session/points/balances/acc-1", {}, {
      getSession: async () => session(),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect((init.headers as Record<string, string>)["X-Journeys-Audit"]).toBeUndefined();
  });
```

Append to `parse-list.test.ts`:

```ts
import { extractContinuationToken } from "./parse-list";

describe("extractContinuationToken", () => {
  it("reads continuationToken", () => {
    expect(extractContinuationToken({ entities: [], continuationToken: "tok" })).toBe("tok");
  });
  it("reads ContinuationToken", () => {
    expect(extractContinuationToken({ Entities: [], ContinuationToken: "tok2" })).toBe("tok2");
  });
  it("returns null when missing", () => {
    expect(extractContinuationToken({ entities: [] })).toBeNull();
  });
});
```

- [ ] **Step 6: `npm test` — expect FAIL** on audit + `extractContinuationToken`.

- [ ] **Step 7: Implement audit + types + parse + queryData**

`api-types.ts` — add `meta?: { continuationToken?: string | null; pageSize?: number }` to `ApiResponse`. Add:

```ts
export type AccountPointBalance = {
  accountId?: string;
  pointAccountTypeId?: string;
  currentBalance?: number;
  lifetimeTotal?: number;
};

export type QueryDataParams = {
  schemaName: string;
  queryString?: string;
  queryArgs?: Record<string, unknown>;
  loyaltyAccountId?: string;
  pageSize?: number;
  continuationToken?: string | null;
  sortBy?: string | null;
  sortOrder?: "ASC" | "DESC";
};
```

Expand `SchemaListItem` with optional `tag?: string` and keep attributes.

`journeys-fetch.ts`:

```ts
export type AdminAuditHeader = {
  loyaltyMemberId: string;
  actionType: string;
  action: string;
  comment?: string;
};

export type JourneysFetchOptions = {
  method?: string;
  body?: unknown;
  searchParams?: Record<string, string | undefined>;
  audit?: AdminAuditHeader;
};
```

After credential headers are set, if `options.audit`:

```ts
  if (options.audit) {
    headers["X-Journeys-Audit"] = JSON.stringify({
      loyaltyMemberId: options.audit.loyaltyMemberId,
      adminUserId: session.userId,
      actionType: options.audit.actionType,
      action: options.audit.action,
      comment: options.audit.comment ?? ""
    });
  }
```

Do **not** add `options.headers`. Export `getJourneysSession` as the existing default session reader (today’s private `defaultGetSession`) so later actions can read `userId` without duplicating it.

`parse-list.ts` — `normalizeSchema` also copy `tag` / `Tag`. Add:

```ts
export function extractContinuationToken(data: unknown): string | null {
  if (!data || typeof data !== "object" || Array.isArray(data)) return null;
  const rec = data as Record<string, unknown>;
  const token = rec.continuationToken ?? rec.ContinuationToken;
  return typeof token === "string" && token.length > 0 ? token : null;
}
```

Replace `queryData` in `actions.ts` (only caller today is the accounts page, rewritten in Task 2):

```ts
export async function queryData<T = Record<string, unknown>>(
  params: QueryDataParams
): Promise<ApiResponse<T[]>> {
  if (!isUsableModelId(params.schemaName)) {
    return {
      success: false,
      error: "A real model name is required; unknown model ids are not sent.",
      timestamp: new Date().toISOString()
    };
  }
  const pageSize = params.pageSize ?? 50;
  const res = await journeysFetch<unknown>(
    `events/${TENANT_SLUG}/${params.schemaName}/admin/query`,
    {
      method: "POST",
      body: {
        query: params.queryString ?? "",
        parameters: params.queryArgs ?? {},
        pageSize,
        continuationToken: params.continuationToken ?? null,
        sortBy: params.sortBy ?? "",
        sortOrder: params.sortOrder ?? "ASC",
        loyaltyAccountId: params.loyaltyAccountId
      }
    }
  );
  if (!res.success) return res as ApiResponse<T[]>;
  return {
    ...res,
    data: extractEntities(res.data) as T[],
    meta: {
      continuationToken: extractContinuationToken(res.data),
      pageSize
    }
  };
}
```

- [ ] **Step 8: `npm test` — PASS. `npx tsc --noEmit` — PASS** (accounts page still using old `queryData(string)` will fail typecheck until Task 2; if so, temporarily keep a deprecated overload **or** do Task 2 list page in the same change set as the signature change). Prefer changing `accounts/page.tsx` in Task 2 immediately after this step if tsc fails.

If tsc fails only on `page.tsx`, convert that file in this task to a thin host that does not call `queryData` yet:

```tsx
export default function AccountsPage() {
  return (
    <>
      <h1>Accounts</h1>
      <p className="empty">Loading accounts…</p>
    </>
  );
}
```

Task 2 replaces it. Do not leave a broken `queryData(schemaName)` call.

- [ ] **Step 9: Do not commit** unless the user asks in that message.

---

