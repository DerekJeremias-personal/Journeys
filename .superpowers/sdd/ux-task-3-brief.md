### Task 3: `journeysFetch` (TDD)

**Files:**
- Create: `Journeys.UX/src/lib/journeys-fetch.ts`
- Test: `Journeys.UX/src/lib/journeys-fetch.test.ts`

**Interfaces:**
- Consumes: `mapLoyaltyPath`, `wrapApiEnvelope`
- Produces: `journeysFetch<T>(path: string, options?: JourneysFetchOptions, deps?: JourneysFetchDeps): Promise<ApiResponse<T>>`
- `JourneysFetchOptions`: `{ method?: string; body?: unknown }`
- `JourneysSession`: `{ userId: string; tenantId: string; accessToken?: string; apiKey?: string }`
- Missing session → `{ success: false, error: "Not authenticated" }` (no network)
- Missing tenantId → `{ success: false, error: "TenantId is required" }` (no network)
- Auth header: if `accessToken` set, `Authorization: Bearer ${accessToken}` only; else if `apiKey` set, `Journeys-API-KEY: ${apiKey}` only; else `{ success: false, error: "No credentials in session" }`
- `JOURNEYS_API_BASE_URL` from `deps.apiBaseUrl` or `process.env.JOURNEYS_API_BASE_URL`; missing → `{ success: false, error: "JOURNEYS_API_BASE_URL is not configured" }`
- Not-allowlisted path → `{ success: false, error }` containing `not-allowlisted` (no network)

- [ ] **Step 1: Write `journeys-fetch.test.ts`**

```ts
import { describe, expect, it, vi } from "vitest";
import { journeysFetch, type JourneysSession } from "./journeys-fetch";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

describe("journeysFetch", () => {
  it("does not call fetch when session is missing", async () => {
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/getall", {}, {
      getSession: async () => null,
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.success).toBe(false);
    expect(r.error).toMatch(/Not authenticated/i);
  });

  it("does not call fetch when path is not allowlisted", async () => {
    const fetchMock = vi.fn();
    const r = await journeysFetch("campaigns/x/save", {}, {
      getSession: async () => session(),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(r.error).toMatch(/not-allowlisted/);
  });

  it("POSTs getall with API key header and wraps entities", async () => {
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ entities: [{ id: "c1", name: "N", status: "Live" }] }), { status: 200 })
    );
    const r = await journeysFetch("campaigns/ignored/getall", { method: "POST", body: { pageSize: 100 } }, {
      getSession: async () => session({ accessToken: undefined, apiKey: "secret-key" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example/api/Campaign/acme/getall");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBe("secret-key");
    expect((init.headers as Record<string, string>)["Authorization"]).toBeUndefined();
    expect(r.success).toBe(true);
  });

  it("sends Bearer when accessToken is present and does not send API key", async () => {
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("schemas/x/model/all", { method: "POST", body: {} }, {
      getSession: async () => session({ accessToken: "tok", apiKey: "should-not-send" }),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example/"
    });
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>)["Authorization"]).toBe("Bearer tok");
    expect((init.headers as Record<string, string>)["Journeys-API-KEY"]).toBeUndefined();
  });
});
```

- [ ] **Step 2: `npm test` — expect FAIL on `journeysFetch`**

- [ ] **Step 3: Implement `journeys-fetch.ts`**

```ts
import type { ApiResponse } from "./api-types";
import { mapLoyaltyPath } from "./map-loyalty-path";
import { wrapApiEnvelope } from "./wrap-api-envelope";

export type JourneysSession = {
  userId: string;
  tenantId: string;
  accessToken?: string;
  apiKey?: string;
};

export type JourneysFetchOptions = {
  method?: string;
  body?: unknown;
};

export type JourneysFetchDeps = {
  getSession: () => Promise<JourneysSession | null>;
  fetch: typeof fetch;
  apiBaseUrl: string;
};

export async function journeysFetch<T>(
  path: string,
  options: JourneysFetchOptions = {},
  deps?: JourneysFetchDeps
): Promise<ApiResponse<T>> {
  const timestamp = new Date().toISOString();
  const resolved: JourneysFetchDeps = deps ?? {
    getSession: defaultGetSession,
    fetch,
    apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? ""
  };
  const session = await resolved.getSession();
  if (!session?.userId) {
    return { success: false, error: "Not authenticated", timestamp };
  }
  if (!session.tenantId?.trim()) {
    return { success: false, error: "TenantId is required", timestamp };
  }
  const base = resolved.apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) {
    return { success: false, error: "JOURNEYS_API_BASE_URL is not configured", timestamp };
  }

  let apiPath: string;
  try {
    apiPath = mapLoyaltyPath(path, session.tenantId);
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    return { success: false, error: message, timestamp };
  }

  const headers: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
  if (session.accessToken) {
    headers.Authorization = `Bearer ${session.accessToken}`;
  } else if (session.apiKey) {
    headers["Journeys-API-KEY"] = session.apiKey;
  } else {
    return { success: false, error: "No credentials in session", timestamp };
  }

  const method = options.method ?? "GET";
  try {
    const res = await resolved.fetch(`${base}${apiPath}`, {
      method,
      headers,
      body: method === "GET" || options.body === undefined ? undefined : JSON.stringify(options.body),
      cache: "no-store"
    });
    const text = await res.text();
    return wrapApiEnvelope(res.status, text) as ApiResponse<T>;
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    return { success: false, error: `Cannot reach Journeys.API (${message})`, timestamp };
  }
}

async function defaultGetSession(): Promise<JourneysSession | null> {
  const { auth } = await import("@/auth");
  const s = await auth();
  if (!s?.user?.id) return null;
  return {
    userId: s.user.id,
    tenantId: (s as { tenantId?: string }).tenantId ?? "",
    accessToken: (s as { accessToken?: string }).accessToken,
    apiKey: (s as { apiKey?: string }).apiKey
  };
}
```

If the `defaultGetSession` dynamic import of `@/auth` makes Vitest load NextAuth during Task 3 tests, keep tests passing by always injecting `deps` (they already do). Do not import `@/auth` at module top level in this file.

- [ ] **Step 4: `npm test` — expect PASS**

- [ ] **Step 5: Do not commit** unless the user asks in that message.

---

