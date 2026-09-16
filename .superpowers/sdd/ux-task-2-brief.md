### Task 2: Allowlist path map + envelope (TDD)

**Files:**
- Create: `Journeys.UX/src/lib/map-loyalty-path.ts`
- Create: `Journeys.UX/src/lib/wrap-api-envelope.ts`
- Create: `Journeys.UX/src/lib/api-types.ts`
- Test: `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- Test: `Journeys.UX/src/lib/wrap-api-envelope.test.ts`

**Interfaces:**
- Consumes: spec §5 allowlist
- Produces: `mapLoyaltyPath(path: string, tenantId: string): string` throws `Error` with message starting `not-allowlisted:` when unknown. `wrapApiEnvelope(status: number, bodyText: string): ApiResponse<unknown>`

- [ ] **Step 1: Create `Journeys.UX/src/lib/api-types.ts`**

```ts
export type ApiResponse<T> = {
  success: boolean;
  data?: T;
  error?: string;
  timestamp: string;
};

export type CampaignListItem = {
  id?: string;
  name?: string;
  status?: string;
  extCampaignId?: string;
};

export type SchemaListItem = {
  id?: string;
  name?: string;
  status?: string;
  attributes?: { name?: string; displayName?: string }[];
};
```

- [ ] **Step 2: Write failing tests `Journeys.UX/src/lib/map-loyalty-path.test.ts`**

```ts
import { describe, expect, it } from "vitest";
import { mapLoyaltyPath } from "./map-loyalty-path";

describe("mapLoyaltyPath", () => {
  it("maps campaigns getall using session tenant, not path slug", () => {
    expect(mapLoyaltyPath("campaigns/slug-from-exp/getall", "acme")).toBe(
      "/api/Campaign/acme/getall"
    );
  });

  it("maps schemas model/all", () => {
    expect(mapLoyaltyPath("schemas/slug/model/all", "acme")).toBe(
      "/api/Model/acme/GetMany"
    );
  });

  it("maps events admin query and preserves schema name", () => {
    expect(mapLoyaltyPath("events/slug/LoyaltyAccountDetails/admin/query", "acme")).toBe(
      "/api/Events/acme/LoyaltyAccountDetails/admin/query"
    );
  });

  it("rejects unknown paths", () => {
    expect(() => mapLoyaltyPath("campaigns/slug/save", "acme")).toThrow(/not-allowlisted:/);
  });

  it("rejects empty tenantId", () => {
    expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
  });
});
```

- [ ] **Step 3: Write failing tests `Journeys.UX/src/lib/wrap-api-envelope.test.ts`**

```ts
import { describe, expect, it } from "vitest";
import { wrapApiEnvelope } from "./wrap-api-envelope";

describe("wrapApiEnvelope", () => {
  it("wraps a JSON array as success data", () => {
    const r = wrapApiEnvelope(200, JSON.stringify([{ id: "1" }]));
    expect(r.success).toBe(true);
    expect(r.data).toEqual([{ id: "1" }]);
  });

  it("wraps a JSON object as success data", () => {
    const r = wrapApiEnvelope(200, JSON.stringify({ entities: [] }));
    expect(r.success).toBe(true);
    expect((r.data as { entities: unknown[] }).entities).toEqual([]);
  });

  it("maps HTTP error JSON error field", () => {
    const r = wrapApiEnvelope(401, JSON.stringify({ error: "nope" }));
    expect(r.success).toBe(false);
    expect(r.error).toBe("nope");
  });

  it("maps non-JSON error body to a short error", () => {
    const r = wrapApiEnvelope(500, "<html>fail</html>");
    expect(r.success).toBe(false);
    expect(r.error).toMatch(/HTTP 500/);
  });

  it("maps empty 200 body as success with undefined data", () => {
    const r = wrapApiEnvelope(200, "");
    expect(r.success).toBe(true);
    expect(r.data).toBeUndefined();
  });
});
```

- [ ] **Step 4: Run tests — expect FAIL**

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Expected: FAIL — modules not found.

- [ ] **Step 5: Implement `map-loyalty-path.ts` and `wrap-api-envelope.ts`**

```ts
export function mapLoyaltyPath(path: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const trimmed = path.replace(/^\/+/, "");

  if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
  }
  if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
    return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
  }
  const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
  if (events) {
    return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
  }
  throw new Error(`not-allowlisted: ${trimmed}`);
}
```

```ts
import type { ApiResponse } from "./api-types";

function nowIso(): string {
  return new Date().toISOString();
}

export function wrapApiEnvelope(status: number, bodyText: string): ApiResponse<unknown> {
  const timestamp = nowIso();
  const trimmed = bodyText.trim();
  let parsed: unknown;
  if (trimmed) {
    try {
      parsed = JSON.parse(trimmed) as unknown;
    } catch {
      parsed = undefined;
    }
  }

  if (status >= 200 && status < 300) {
    return { success: true, data: trimmed ? parsed : undefined, timestamp };
  }

  let error = `HTTP ${status}`;
  if (parsed && typeof parsed === "object" && parsed !== null && "error" in parsed) {
    const e = (parsed as { error: unknown }).error;
    if (typeof e === "string" && e.trim()) error = e;
  } else if (!parsed && trimmed) {
    error = `HTTP ${status}: ${trimmed.slice(0, 180)}`;
  }
  return { success: false, error, timestamp };
}
```

- [ ] **Step 6: Re-run `npm test` — expect PASS**

- [ ] **Step 7: Do not commit** unless the user asks in that message.

---

