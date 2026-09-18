# Journeys.UX unlabeled campaign agent chat Implementation Plan

> **Execution:** After approval, say execute and the intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not** use superpowers:subagent-driven-development. Linear unit issues land before any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Show an unlabeled Campaign Agent chat in `Journeys.UX` that streams one turn through a Next SSE proxy to existing `Journeys.API` (Development = Ollama).

**Architecture:** Dedicated App Router `POST` pipes `text/event-stream`. `journeysFetch` stays JSON-only. Browser never sees API keys. Thin chat page; lift EXP `pushSseBytes` and keepalive; trim the early-proxy to Node `fetch` + existing localhost TLS helper (do not add `undici`, do not lift coach copy).

**Tech Stack:** Next.js 16, React 19, NextAuth 5, Vitest. Existing `Journeys.API` Campaign Agent SSE (`started` / `delta` / `done` | `error`).

**Spec:** `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- `Journeys.UX` must not project-reference Core, DAL, or Infra. HTTP to `Journeys.API` only.
- Never treat Auth0 `"hayward"` as `TenantId`. Use `resolveTenantId` (`JOURNEYS_TENANT_ID` wins).
- In-product name is **agent**. Do not ship “Campaign Coach” or other product names.
- Do not implement kebab, wizard, Journey Builder, copy/restore, resume banner, JSON inspector, clear-session, or linked-campaign picker.
- Do not change `CampaignAgent:Provider` or add HintPath to `Backend.Llm.OpenAICompatible`.
- Do not add `undici` or `@exp/*`. Do not wrap SSE in `{ success, data, error }`.
- Never log API keys, JWT, prompt text, or full SSE bodies.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.UX/src/lib/campaign-agent/stream-url.ts` | Build upstream stream URL |
| `Journeys.UX/src/lib/campaign-agent/sse.ts` | `pushSseBytes` (lift EXP) |
| `Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts` | Idle SSE comment heartbeat (lift EXP) |
| `Journeys.UX/src/lib/campaign-agent/proxy-auth.ts` | Session gate + upstream headers |
| `Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts` | Pipe upstream SSE; map connect/HTTP failures to `error` |
| `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts` | Next POST proxy |
| `Journeys.UX/src/lib/campaign-agent/chat-state.ts` | Pure SSE → transcript reducer |
| `Journeys.UX/src/components/loyalty/agent-chat.tsx` | Thin chat client |
| `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx` | Agent page |
| `Journeys.UX/src/app/loyalty/campaigns/page.tsx` | **Agent** link |
| `docs/developer/journeys-ux.md` | How to open Agent + live smoke |
| `docs/developer/campaign-agent-llm.md` | UX hosts the chat |
| `docs/product/graph/path-map.yaml` | `Journeys.UX` → `[campaigns, campaign-agent]` |
| `docs/platform/architecture.md` | One sentence: UX SSE proxy is HTTP client, not write authority |
| `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` | Status → Approved after this plan is executed |

Do not modify `map-loyalty-path.ts` (JSON allowlist only).

---

### Task 1: Upstream stream URL

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/stream-url.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/stream-url.test.ts`

**Interfaces:**
- Consumes: spec §4 URL shape
- Produces: `campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from "vitest";
import { campaignAgentStreamUrl } from "./stream-url";

describe("campaignAgentStreamUrl", () => {
  it("maps to Journeys.API campaign-agent stream", () => {
    expect(campaignAgentStreamUrl("https://127.0.0.1:7001", "TestTenant1")).toBe(
      "https://127.0.0.1:7001/api/v1/TestTenant1/campaign-agent/messages/stream"
    );
  });

  it("trims trailing slash on the base URL and encodes tenant", () => {
    expect(campaignAgentStreamUrl("https://api.example/", "ac me")).toBe(
      "https://api.example/api/v1/ac%20me/campaign-agent/messages/stream"
    );
  });

  it("rejects empty tenantId", () => {
    expect(() => campaignAgentStreamUrl("https://api.example", "  ")).toThrow(/tenant/i);
  });

  it("rejects empty base URL", () => {
    expect(() => campaignAgentStreamUrl("  ", "acme")).toThrow(/JOURNEYS_API_BASE_URL|base/i);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
Working directory: `Journeys.UX`  
Expected: FAIL (module not found)

- [ ] **Step 3: Implement**

```ts
export function campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const base = apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) throw new Error("JOURNEYS_API_BASE_URL is not configured");
  return `${base}/api/v1/${encodeURIComponent(tenant)}/campaign-agent/messages/stream`;
}
```

- [ ] **Step 4: Run tests**

Run: `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
Expected: PASS

- [ ] **Step 5: Commit** — skip unless the user asks.

---

### Task 2: SSE byte parser

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/sse.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/sse.test.ts`

**Interfaces:**
- Consumes: EXP `temp/exp/apps/admin-web/src/lib/campaign-agent/sse.ts` (copy the parser only)
- Produces: `export type SseEvent = { event: string; data: string }` and `pushSseBytes(buffer: string, chunk: string): { buffer: string; events: SseEvent[] }`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from "vitest";
import { pushSseBytes } from "./sse";

describe("pushSseBytes", () => {
  it("parses started, delta, done, and error blocks", () => {
    const chunk =
      'event: started\ndata: {"conversationId":"c1"}\n\n' +
      'event: delta\ndata: {"text":"Hi"}\n\n' +
      'event: done\ndata: {"conversationId":"c1"}\n\n' +
      'event: error\ndata: {"message":"boom"}\n\n';
    const { buffer, events } = pushSseBytes("", chunk);
    expect(buffer).toBe("");
    expect(events.map((e) => e.event)).toEqual(["started", "delta", "done", "error"]);
    expect(events[0].data).toContain("c1");
    expect(events[1].data).toContain("Hi");
    expect(events[3].data).toContain("boom");
  });

  it("holds a partial block in the buffer", () => {
    const first = pushSseBytes("", "event: delta\ndata: {\"text\":");
    expect(first.events).toEqual([]);
    const second = pushSseBytes(first.buffer, "\"x\"}\n\n");
    expect(second.events).toEqual([{ event: "delta", data: '{"text":"x"}' }]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- src/lib/campaign-agent/sse.test.ts`  
Expected: FAIL (module not found)

- [ ] **Step 3: Copy `pushSseBytes` from EXP `sse.ts` into `Journeys.UX/src/lib/campaign-agent/sse.ts`** (the full function and `SseEvent` type; no coach imports).

- [ ] **Step 4: Run tests**

Run: `npm test -- src/lib/campaign-agent/sse.test.ts`  
Expected: PASS

- [ ] **Step 5: Commit** — skip unless the user asks.

---

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

Early proxy: **do not** copy EXP `undici` / coach strings. Default `connect` is `fetch`. On connect throw, enqueue SSE `error` with `Cannot reach Journeys.API (${describeFetchFailure(error)})`. On HTTP status not 2xx, enqueue `error` with truncated body (max 500 chars) or `not authorized / check tenant or key` when status is 401 or 403. On success, pipe `response.body` bytes through `wrapStreamWithSseKeepAlive`. Waiting-upstream label, if emitted, is `Waiting for agent…` (never “Campaign Coach”).

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

- [ ] **Step 9: Commit** — skip unless the user asks.

---

### Task 4: Next stream route

**Files:**
- Create: `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts`
- Test: `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.test.ts`

**Interfaces:**
- Consumes: `auth` from `@/auth` (same session shape as `journeysFetch` `defaultGetSession`); `resolveTenantId`; Task 1–3 helpers; `allowInsecureLocalHttps`
- Produces: `POST(request: Request): Promise<Response>`  
  Body JSON `{ message: string; conversationId?: string | null }`. Omit `linkedCampaignId`. Empty `message` → 400 `{ error: "message is required" }`. No session → 401 JSON `{ error: "Not authenticated" }` **without** calling fetch. Success → `200` `text/event-stream` from `createEarlySseProxyStream`.

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

If mocking the proxy module is brittle, instead export `handleCampaignAgentStreamPost(request, deps)` from the route file (or `stream-route.ts` next to it) with `getSession`, `apiBaseUrl`, `connect` injected — **prefer that**. Put `handleCampaignAgentStreamPost` in `Journeys.UX/src/lib/campaign-agent/stream-route.ts` and have `route.ts` call it with real `auth` + `fetch`.

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
2. `getSession()` → `authorizeCampaignAgentProxy`; 401 JSON if not ok.
3. `resolveTenantId(session.tenantId)`; 400 if empty.
4. `campaignAgentStreamUrl(apiBaseUrl, tenantId)`.
5. `allowInsecureLocalHttps(apiBaseUrl)`.
6. Body to upstream: `{ message: trimmed, conversationId: body.conversationId || undefined }` — no `linkedCampaignId`.
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

- [ ] **Step 5: Commit** — skip unless the user asks.

---

### Task 5: Chat state, page, and Campaigns link

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/chat-state.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/chat-state.test.ts`
- Create: `Journeys.UX/src/components/loyalty/agent-chat.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/page.tsx` — add **Agent** link above the list
- Modify: `Journeys.UX/src/app/globals.css` — `.agent-chat` transcript + form spacing only

**Interfaces:**
- Consumes: `SseEvent`, `pushSseBytes`; POST `/api/loyalty/campaign-agent/messages/stream`
- Produces:
  - `export type ChatLine = { role: "user" | "assistant" | "error"; text: string }`
  - `export type AgentChatState = { conversationId: string | null; lines: ChatLine[]; streaming: boolean }`
  - `applyAgentSseEvent(state: AgentChatState, ev: SseEvent): AgentChatState`
  - `beginUserTurn(state: AgentChatState, message: string): AgentChatState`
  - Page heading exactly `Agent`. Placeholder: `Describe what you want to build or change…`

Reducer rules:
- `started`: set `conversationId` from JSON `conversationId` if present.
- `delta`: append JSON `text` to the last `assistant` line, or push a new assistant line.
- `done`: `streaming: false`. Keep `conversationId`.
- `error`: push `{ role: "error", text: message }`, `streaming: false`, **keep** `conversationId`.
- `progress`, `proxy`, other events: return state unchanged.
- `beginUserTurn`: append user line, `streaming: true`.
- Empty trimmed message: caller does not call begin/fetch (no-op).

- [ ] **Step 1: Write failing chat-state tests**

```ts
import { describe, expect, it } from "vitest";
import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "./chat-state";

const empty: AgentChatState = { conversationId: null, lines: [], streaming: false };

describe("applyAgentSseEvent", () => {
  it("stores conversationId from started and keeps it on error", () => {
    let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"conv-1"}' });
    expect(s.conversationId).toBe("conv-1");
    s = applyAgentSseEvent(s, { event: "error", data: '{"message":"tool failed"}' });
    expect(s.conversationId).toBe("conv-1");
    expect(s.streaming).toBe(false);
    expect(s.lines.at(-1)).toEqual({ role: "error", text: "tool failed" });
  });

  it("appends delta text onto one assistant line", () => {
    let s = beginUserTurn(empty, "hi");
    s = applyAgentSseEvent(s, { event: "delta", data: '{"text":"Hel"}' });
    s = applyAgentSseEvent(s, { event: "delta", data: '{"text":"lo"}' });
    s = applyAgentSseEvent(s, { event: "done", data: '{"conversationId":"c"}' });
    expect(s.lines).toEqual([
      { role: "user", text: "hi" },
      { role: "assistant", text: "Hello" }
    ]);
    expect(s.streaming).toBe(false);
  });

  it("ignores progress events", () => {
    const s = applyAgentSseEvent(empty, { event: "progress", data: '{"label":"x"}' });
    expect(s).toEqual(empty);
  });
});
```

- [ ] **Step 2: Run to verify fail**

Run: `npm test -- src/lib/campaign-agent/chat-state.test.ts`  
Expected: FAIL

- [ ] **Step 3: Implement `chat-state.ts`**

- [ ] **Step 4: Tests pass**

Run: `npm test -- src/lib/campaign-agent/chat-state.test.ts`  
Expected: PASS

- [ ] **Step 5: Implement `agent-chat.tsx` (client)**  
`useState` for input + `AgentChatState`. On submit: trim; if empty or `streaming`, return; `beginUserTurn`; `fetch("/api/loyalty/campaign-agent/messages/stream", { method: "POST", headers: { "Content-Type": "application/json", Accept: "text/event-stream" }, body: JSON.stringify({ message, conversationId: state.conversationId }), cache: "no-store" })`. If `!res.ok` and not event-stream, apply an error line (`not authorized / check tenant or key` for 401/403). Else read `res.body` with `pushSseBytes` + `applyAgentSseEvent`. Disable the send button while `streaming`. Show `conversationId` in small text when set. No coach strings.

- [ ] **Step 6: Agent page**

```tsx
import { AgentChat } from "@/components/loyalty/agent-chat";

export default function CampaignAgentPage() {
  return (
    <>
      <h1>Agent</h1>
      <AgentChat />
    </>
  );
}
```

- [ ] **Step 7: Campaigns list link** — at the top of `campaigns/page.tsx`, next to the heading:

```tsx
import Link from "next/link";
// ...
<div>
  <h1>Campaigns</h1>
  <p><Link href="/loyalty/campaigns/agent">Agent</Link></p>
</div>
```

- [ ] **Step 8: Run full UX tests**

Run: `npm test`  
Expected: PASS (including existing 38 + new files)

- [ ] **Step 9: Commit** — skip unless the user asks.

---

### Task 6: Docs and graph

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/campaign-agent-llm.md`
- Modify: `docs/product/graph/path-map.yaml` (`Journeys.UX` nodes: `[campaigns, campaign-agent]`)
- Modify: `docs/platform/architecture.md` — UI row / Journeys.UX sentence: SSE proxy is an HTTP client of Campaign Agent; UX is not the write authority
- Modify: `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` — `Status: Approved (human 2026-09-17)`
- Touch `docs/platform/overlays.md` only if `docs-impact` requires it in the change set; prefer a one-line pointer that UX may proxy Campaign Agent SSE. Do **not** add `campaign-agent` `IMPLEMENTED_AS` `proj-ux` in `edges.yaml`.

**Interfaces:**
- Consumes: spec §8
- Produces: `docs-impact` and `graph-impact` exit 0 on this change set

- [ ] **Step 1: Update `path-map.yaml`**

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, campaign-agent]
    meaningOptional: false
```

- [ ] **Step 2: Add to `journeys-ux.md` after the screens table**

```
## Agent

Unlabeled chat at `/loyalty/campaigns/agent` (link from Campaigns). Browser POSTs to Next `/api/loyalty/campaign-agent/messages/stream`; the server forwards to `POST /api/v1/{tenantId}/campaign-agent/messages/stream`. Secrets stay on the Next server. LLM provider is API startup config (`docs/developer/campaign-agent-llm.md`), not a UX control.

Live smoke: API + Ollama tray → sign in → Campaigns → Agent → one turn with streamed assistant text and at least one successful MCP tool. Not required: Draft upsert or Live publish.
```

- [ ] **Step 3: Add one line to `campaign-agent-llm.md` Live smoke:** UX at `/loyalty/campaigns/agent` is the operator surface; provider switch is unchanged.

- [ ] **Step 4: Architecture one-liner** that `Journeys.UX` may proxy Campaign Agent SSE and still must not own campaign writes.

- [ ] **Step 5: Run impact scripts from `Journeys/` with the changed file list**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  'Journeys.UX/src/lib/campaign-agent/stream-url.ts',
  'Journeys.UX/src/lib/campaign-agent/sse.ts',
  'Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts',
  'Journeys.UX/src/lib/campaign-agent/proxy-auth.ts',
  'Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts',
  'Journeys.UX/src/lib/campaign-agent/stream-route.ts',
  'Journeys.UX/src/lib/campaign-agent/chat-state.ts',
  'Journeys.UX/src/components/loyalty/agent-chat.tsx',
  'Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts',
  'Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx',
  'Journeys.UX/src/app/loyalty/campaigns/page.tsx',
  'docs/developer/journeys-ux.md',
  'docs/developer/campaign-agent-llm.md',
  'docs/platform/architecture.md',
  'docs/platform/overlays.md',
  'docs/product/graph/path-map.yaml'
)
.\scripts\graph-impact.ps1 -Files @(
  'Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx',
  'Journeys.UX/src/lib/campaign-agent/stream-route.ts',
  'docs/product/graph/path-map.yaml'
)
```

Expected: both exit 0. If docs-impact names a missing doc, add the pointer in that file and re-run.

- [ ] **Step 6: Commit** — skip unless the user asks.

---

### Task 7: Live smoke (G3)

**Files:** none (manual / browser)

**Interfaces:**
- Consumes: running `Journeys.API` with `CampaignAgent:Provider` = `Ollama`, MSI Ollama tray, `Journeys.UX` `npm run dev`, signed-in session (`TestTenant1`)

- [ ] **Step 1: Confirm API swagger** `https://127.0.0.1:7001/swagger/index.html` returns 200. If it hangs, restart the API in Visual Studio.

- [ ] **Step 2: Confirm Ollama** `http://192.168.1.191:11434` is up (tray on the MSI). Restart the API if Provider was just switched.

- [ ] **Step 3: Browser** sign in → `/loyalty/campaigns` → **Agent** → send e.g. `List campaigns for this tenant.` Pass: streamed assistant text **and** a successful MCP tool (`list_campaigns` or digest). Fail: silent hang, Coach branding, API key in the browser network tab, or `modelId` `"unknown"`.

- [ ] **Step 4: Run** `npm test` in `Journeys.UX` once more. Expected: PASS.

- [ ] **Step 5: Commit** — skip unless the user asks.

---

## Self-review (plan vs spec)

| Spec | Task |
|------|------|
| G1 Agent link + `/loyalty/campaigns/agent` | 5 |
| G2 streamed turn via Next proxy | 3–5 |
| G3 live MCP bar | 7 |
| G4 secrets server-side | 3–4 (no EventSource to API) |
| G5 no provider UI | 5 (chat only) |
| `journeysFetch` unchanged | 1 (separate URL helper) |
| Tool-audit stays on API | no UX audit files |
| Reload = new thread | 5 (state is React-only) |
| Docs/graph | 6 |
| Full IA not in this increment | no kebab/wizard/copy files |
