### Task 4: Next stream route

**Files:**
- Create: `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts`
- Test: `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.test.ts`

**Interfaces:**
- Consumes: `auth` from `@/auth` (same session shape as `journeysFetch` `defaultGetSession`); `resolveTenantId`; Task 1â€“3 helpers; `allowInsecureLocalHttps`
- Produces: `POST(request: Request): Promise<Response>`  
  Body JSON `{ message: string; conversationId?: string | null }`. Omit `linkedCampaignId`. Empty `message` â†’ 400 `{ error: "message is required" }`. No session â†’ 401 JSON `{ error: "Not authenticated" }` **without** calling fetch. Success â†’ `200` `text/event-stream` from `createEarlySseProxyStream`.

Log once when the stream ends: `{ tenantId, conversationId, userId, status }` via `console.info`. Never log `message` or headers.

```ts
export const dynamic = "force-dynamic";
export const maxDuration = 600;
```

SSE response headers:

```
Content-Type: text/event-stream; charset=utf-8
Cache-Control: no-cache, no-transform
Connection: keep-alive
X-Accel-Buffering: no
```

- [ ] **Step 1: Write failing route tests** (mock `auth` with `vi.mock("@/auth", ...)`)

```ts
import { beforeEach, describe, expect, it, vi } from "vitest";

const auth = vi.fn();
vi.mock("@/auth", () => ({ auth: (...args: unknown[]) => auth(...args) }));

const connect = vi.fn();
vi.mock("@/lib/campaign-agent/sse-early-proxy", async () => {
  const actual = await vi.importActual<typeof import("@/lib/campaign-agent/sse-early-proxy")>(
    "@/lib/campaign-agent/sse-early-proxy"
  );
  return {
    ...actual,
    createEarlySseProxyStream: (opts: { connect?: typeof fetch }) =>
      actual.createEarlySseProxyStream({
        ...opts,
        connect: opts.connect ?? (connect as unknown as typeof fetch),
        idleMs: 60_000
      })
  };
});
```

If mocking the proxy module is brittle, instead export `handleCampaignAgentStreamPost(request, deps)` from the route file (or `stream-route.ts` next to it) with `getSession`, `apiBaseUrl`, `connect` injected â€” **prefer that**. Put `handleCampaignAgentStreamPost` in `Journeys.UX/src/lib/campaign-agent/stream-route.ts` and have `route.ts` call it with real `auth` + `fetch`.

Test file then targets `stream-route.ts`:

```ts
import { describe, expect, it, vi } from "vitest";
import { handleCampaignAgentStreamPost } from "./stream-route";

describe("handleCampaignAgentStreamPost", () => {
  it("returns 401 and does not call connect when session is missing", async () => {
    const connect = vi.fn();
    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/api/loyalty/campaign-agent/messages/stream", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: "hello" })
    }), {
      getSession: async () => null,
      apiBaseUrl: "https://example.test",
      connect: connect as unknown as typeof fetch
    });
    expect(res.status).toBe(401);
    expect(connect).not.toHaveBeenCalled();
    await expect(res.json()).resolves.toMatchObject({ error: expect.stringMatching(/not authenticated/i) });
  });

  it("returns 400 when message is empty", async () => {
    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/x", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: "  " })
    }), {
      getSession: async () => ({ userId: "u1", tenantId: "acme", apiKey: "k" }),
      apiBaseUrl: "https://example.test",
      connect: vi.fn() as unknown as typeof fetch
    });
    expect(res.status).toBe(400);
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `npm test -- src/lib/campaign-agent/stream-route.test.ts`  
Expected: FAIL

- [ ] **Step 3: Implement `stream-route.ts` + thin `route.ts`**

`handleCampaignAgentStreamPost`:
1. Parse JSON; 400 on invalid JSON or blank `message`.
2. `getSession()` â†’ `authorizeCampaignAgentProxy`; 401 JSON if not ok.
3. `resolveTenantId(session.tenantId)`; 400 if empty.
4. `campaignAgentStreamUrl(apiBaseUrl, tenantId)`.
5. `allowInsecureLocalHttps(apiBaseUrl)`.
6. Body to upstream: `{ message: trimmed, conversationId: body.conversationId || undefined }` â€” no `linkedCampaignId`.
7. Return `new Response(createEarlySseProxyStream({...}), { status: 200, headers: SSE_HEADERS })`.

`route.ts`:

```ts
import { auth } from "@/auth";
import { handleCampaignAgentStreamPost } from "@/lib/campaign-agent/stream-route";
import type { JourneysSession } from "@/lib/journeys-fetch";

export const dynamic = "force-dynamic";
export const maxDuration = 600;

async function getSession(): Promise<JourneysSession | null> {
  const s = await auth();
  if (!s?.user?.id) return null;
  return {
    userId: s.user.id,
    tenantId: (s as { tenantId?: string }).tenantId ?? "",
    accessToken: (s as { accessToken?: string }).accessToken,
    apiKey: (s as { apiKey?: string }).apiKey
  };
}

export async function POST(request: Request) {
  return handleCampaignAgentStreamPost(request, {
    getSession,
    apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? "",
    connect: fetch
  });
}
```

- [ ] **Step 4: Run tests including existing fetch tests**

Run: `npm test`  
Working directory: `Journeys.UX`  
Expected: all previous tests still PASS, new stream-route tests PASS

- [ ] **Step 5: Commit** â€” skip unless the user asks.

---