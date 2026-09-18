### Task 3: Proxy auth, headers, and early SSE pipe

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/proxy-auth.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/proxy-auth.test.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/sse-early-proxy.test.ts`

**Interfaces:**
- Consumes: `JourneysSession` from `src/lib/journeys-fetch.ts`; `allowInsecureLocalHttps` / `describeFetchFailure` from `src/lib/local-https.ts`
- Produces:
  - `authorizeCampaignAgentProxy(session: JourneysSession | null): { ok: true; session: JourneysSession } | { ok: false; status: 401; error: string }`
  - `campaignAgentUpstreamHeaders(session: JourneysSession): Record<string, string>`
  - `createEarlySseProxyStream(options: { upstreamUrl: string; upstreamInit: RequestInit; idleMs?: number; signal?: AbortSignal; connect?: typeof fetch }): ReadableStream<Uint8Array>`
  - `wrapStreamWithSseKeepAlive` from lifted keepalive

Copy keepalive from EXP `sse-keepalive.ts` unchanged except file path.

Early proxy: **do not** copy EXP `undici` / coach strings. Default `connect` is `fetch`. On connect throw, enqueue SSE `error` with `Cannot reach Journeys.API (${describeFetchFailure(error)})`. On HTTP status not 2xx, enqueue `error` with truncated body (max 500 chars) or `not authorized / check tenant or key` when status is 401 or 403. On success, pipe `response.body` bytes through `wrapStreamWithSseKeepAlive`. Waiting-upstream label, if emitted, is `Waiting for agentâ€¦` (never â€œCampaign Coachâ€).

- [ ] **Step 1: Write failing auth tests**

```ts
import { describe, expect, it } from "vitest";
import { authorizeCampaignAgentProxy, campaignAgentUpstreamHeaders } from "./proxy-auth";
import type { JourneysSession } from "../journeys-fetch";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

describe("authorizeCampaignAgentProxy", () => {
  it("rejects missing session without calling upstream", () => {
    const r = authorizeCampaignAgentProxy(null);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.status).toBe(401);
  });

  it("rejects session with no credentials", () => {
    const r = authorizeCampaignAgentProxy(session({ apiKey: undefined, accessToken: undefined }));
    expect(r.ok).toBe(false);
  });

  it("accepts API-key session", () => {
    const r = authorizeCampaignAgentProxy(session());
    expect(r.ok).toBe(true);
  });
});

describe("campaignAgentUpstreamHeaders", () => {
  it("sends Journeys-API-KEY when there is no access token", () => {
    const h = campaignAgentUpstreamHeaders(session({ apiKey: "secret-key", accessToken: undefined }));
    expect(h["Journeys-API-KEY"]).toBe("secret-key");
    expect(h.Authorization).toBeUndefined();
    expect(h.Accept).toBe("text/event-stream");
  });

  it("sends Bearer and omits API key when accessToken is present", () => {
    const h = campaignAgentUpstreamHeaders(session({ accessToken: "tok", apiKey: "should-not-send" }));
    expect(h.Authorization).toBe("Bearer tok");
    expect(h["Journeys-API-KEY"]).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `npm test -- src/lib/campaign-agent/proxy-auth.test.ts`  
Expected: FAIL (module not found)

- [ ] **Step 3: Implement `proxy-auth.ts`**

```ts
import type { JourneysSession } from "../journeys-fetch";

export type CampaignAgentProxyAuth =
  | { ok: true; session: JourneysSession }
  | { ok: false; status: 401; error: string };

export function authorizeCampaignAgentProxy(session: JourneysSession | null): CampaignAgentProxyAuth {
  if (!session?.userId) return { ok: false, status: 401, error: "Not authenticated" };
  if (!session.accessToken && !session.apiKey) {
    return { ok: false, status: 401, error: "No credentials in session" };
  }
  return { ok: true, session };
}

export function campaignAgentUpstreamHeaders(session: JourneysSession): Record<string, string> {
  const headers: Record<string, string> = {
    Accept: "text/event-stream",
    "Content-Type": "application/json"
  };
  if (session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`;
  else if (session.apiKey) headers["Journeys-API-KEY"] = session.apiKey;
  return headers;
}
```

- [ ] **Step 4: Auth tests pass**

Run: `npm test -- src/lib/campaign-agent/proxy-auth.test.ts`  
Expected: PASS

- [ ] **Step 5: Write failing early-proxy test**

```ts
import { describe, expect, it, vi } from "vitest";
import { createEarlySseProxyStream } from "./sse-early-proxy";
import { pushSseBytes } from "./sse";

async function readAll(stream: ReadableStream<Uint8Array>): Promise<string> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let out = "";
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    if (value) out += decoder.decode(value, { stream: true });
  }
  return out;
}

describe("createEarlySseProxyStream", () => {
  it("maps 401 to an SSE error and does not throw", async () => {
    const connect = vi.fn(async () => new Response(JSON.stringify({ error: "nope" }), { status: 401 }));
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    const { events } = pushSseBytes("", text);
    const err = events.find((e) => e.event === "error");
    expect(err).toBeTruthy();
    expect(err!.data.toLowerCase()).toMatch(/not authorized|nope|401/);
  });

  it("maps connect failure to Cannot reach Journeys.API", async () => {
    const connect = vi.fn(async () => {
      throw new Error("fetch failed");
    });
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    expect(text).toMatch(/Cannot reach Journeys\.API/);
    expect(text).not.toMatch(/Coach/i);
  });

  it("forwards upstream SSE bytes on 200 event-stream", async () => {
    const body = "event: started\ndata: {\"conversationId\":\"c1\"}\n\n";
    const connect = vi.fn(
      async () =>
        new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream; charset=utf-8" } })
    );
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    expect(text).toContain("event: started");
    expect(text).toContain("c1");
  });
});
```

- [ ] **Step 6: Run to verify fail**

Run: `npm test -- src/lib/campaign-agent/sse-early-proxy.test.ts`  
Expected: FAIL

- [ ] **Step 7: Copy EXP `sse-keepalive.ts` into `Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts`.** Implement `sse-early-proxy.ts` with injectable `connect` (default `fetch`), keepalive wrap, SSE `error` encoding `event: error\ndata: {"message":"..."}\n\n`. Do not log request bodies or keys.

- [ ] **Step 8: Run proxy + auth tests**

Run: `npm test -- src/lib/campaign-agent/proxy-auth.test.ts src/lib/campaign-agent/sse-early-proxy.test.ts`  
Expected: PASS

- [ ] **Step 9: Commit** â€” skip unless the user asks.

---