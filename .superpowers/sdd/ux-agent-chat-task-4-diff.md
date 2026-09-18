# Review package Task 4 (after abort-log fix)
Base: uncommitted
Head: working tree

diff --git a/Journeys.UX/src/lib/campaign-agent/stream-route.ts b/Journeys.UX/src/lib/campaign-agent/stream-route.ts
new file mode 100644
index 0000000..3b2b8f9
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/stream-route.ts
@@ -0,0 +1,124 @@
+import type { JourneysSession } from "../journeys-fetch";
+import { allowInsecureLocalHttps } from "../local-https";
+import { resolveTenantId } from "../resolve-tenant-id";
+import { authorizeCampaignAgentProxy, campaignAgentUpstreamHeaders } from "./proxy-auth";
+import { createEarlySseProxyStream } from "./sse-early-proxy";
+import { campaignAgentStreamUrl } from "./stream-url";
+
+export type CampaignAgentStreamRouteDeps = {
+  getSession: () => Promise<JourneysSession | null>;
+  apiBaseUrl: string;
+  connect: typeof fetch;
+};
+
+const SSE_HEADERS = {
+  "Content-Type": "text/event-stream; charset=utf-8",
+  "Cache-Control": "no-cache, no-transform",
+  Connection: "keep-alive",
+  "X-Accel-Buffering": "no"
+} as const;
+
+type StreamBody = {
+  message?: string;
+  conversationId?: string | null;
+};
+
+function jsonError(status: number, error: string): Response {
+  return Response.json({ error }, { status });
+}
+
+function logWhenStreamEnds(
+  stream: ReadableStream<Uint8Array>,
+  payload: {
+    tenantId: string;
+    conversationId: string | undefined;
+    userId: string;
+    status: () => number;
+  }
+): ReadableStream<Uint8Array> {
+  let logged = false;
+  const logOnce = (): void => {
+    if (logged) return;
+    logged = true;
+    console.info({
+      tenantId: payload.tenantId,
+      conversationId: payload.conversationId,
+      userId: payload.userId,
+      status: payload.status()
+    });
+  };
+
+  return stream.pipeThrough(
+    new TransformStream<Uint8Array, Uint8Array>({
+      flush() {
+        logOnce();
+      },
+      cancel() {
+        logOnce();
+      }
+    })
+  );
+}
+
+export async function handleCampaignAgentStreamPost(
+  request: Request,
+  deps: CampaignAgentStreamRouteDeps
+): Promise<Response> {
+  let body: StreamBody;
+  try {
+    body = (await request.json()) as StreamBody;
+  } catch {
+    return jsonError(400, "Invalid JSON body");
+  }
+
+  const trimmed = body.message?.trim() ?? "";
+  if (!trimmed) {
+    return jsonError(400, "message is required");
+  }
+
+  const auth = authorizeCampaignAgentProxy(await deps.getSession());
+  if (!auth.ok) {
+    return jsonError(auth.status, auth.error);
+  }
+
+  const tenantId = resolveTenantId(auth.session.tenantId);
+  if (!tenantId) {
+    return jsonError(400, "TenantId is required");
+  }
+
+  const upstreamUrl = campaignAgentStreamUrl(deps.apiBaseUrl, tenantId);
+  allowInsecureLocalHttps(deps.apiBaseUrl);
+
+  const conversationId = body.conversationId || undefined;
+  let status = 0;
+  const connect: typeof fetch = async (input, init) => {
+    const response = await deps.connect(input, init);
+    status = response.status;
+    return response;
+  };
+
+  const stream = createEarlySseProxyStream({
+    upstreamUrl,
+    upstreamInit: {
+      method: "POST",
+      headers: campaignAgentUpstreamHeaders(auth.session),
+      cache: "no-store",
+      body: JSON.stringify({
+        message: trimmed,
+        conversationId
+      })
+    },
+    signal: request.signal,
+    connect
+  });
+
+  return new Response(
+    logWhenStreamEnds(stream, {
+      tenantId,
+      conversationId,
+      userId: auth.session.userId,
+      status: () => status
+    }),
+    { status: 200, headers: SSE_HEADERS }
+  );
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts b/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts
new file mode 100644
index 0000000..5a4c6b4
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts
@@ -0,0 +1,207 @@
+import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
+import type { JourneysSession } from "../journeys-fetch";
+import { handleCampaignAgentStreamPost } from "./stream-route";
+
+function session(partial: Partial<JourneysSession> = {}): JourneysSession {
+  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
+}
+
+function jsonRequest(body: unknown, init: RequestInit = {}): Request {
+  return new Request("http://localhost/api/loyalty/campaign-agent/messages/stream", {
+    method: "POST",
+    headers: { "Content-Type": "application/json" },
+    body: JSON.stringify(body),
+    ...init
+  });
+}
+
+async function readResponseText(res: Response): Promise<string> {
+  return res.text();
+}
+
+describe("handleCampaignAgentStreamPost", () => {
+  beforeEach(() => {
+    vi.stubEnv("JOURNEYS_TENANT_ID", "");
+  });
+
+  afterEach(() => {
+    vi.unstubAllEnvs();
+    vi.restoreAllMocks();
+  });
+
+  it("returns 401 and does not call connect when session is missing", async () => {
+    const connect = vi.fn();
+    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/api/loyalty/campaign-agent/messages/stream", {
+      method: "POST",
+      headers: { "Content-Type": "application/json" },
+      body: JSON.stringify({ message: "hello" })
+    }), {
+      getSession: async () => null,
+      apiBaseUrl: "https://example.test",
+      connect: connect as unknown as typeof fetch
+    });
+    expect(res.status).toBe(401);
+    expect(connect).not.toHaveBeenCalled();
+    await expect(res.json()).resolves.toMatchObject({ error: expect.stringMatching(/not authenticated/i) });
+  });
+
+  it("returns 400 when message is empty", async () => {
+    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/x", {
+      method: "POST",
+      headers: { "Content-Type": "application/json" },
+      body: JSON.stringify({ message: "  " })
+    }), {
+      getSession: async () => ({ userId: "u1", tenantId: "acme", apiKey: "k" }),
+      apiBaseUrl: "https://example.test",
+      connect: vi.fn() as unknown as typeof fetch
+    });
+    expect(res.status).toBe(400);
+    await expect(res.json()).resolves.toMatchObject({ error: "message is required" });
+  });
+
+  it("returns 400 on invalid JSON without calling connect", async () => {
+    const connect = vi.fn();
+    const res = await handleCampaignAgentStreamPost(
+      new Request("http://localhost/x", {
+        method: "POST",
+        headers: { "Content-Type": "application/json" },
+        body: "{not-json"
+      }),
+      {
+        getSession: async () => session(),
+        apiBaseUrl: "https://example.test",
+        connect: connect as unknown as typeof fetch
+      }
+    );
+    expect(res.status).toBe(400);
+    expect(connect).not.toHaveBeenCalled();
+  });
+
+  it("returns 401 and does not call connect when session has no credentials", async () => {
+    const connect = vi.fn();
+    const res = await handleCampaignAgentStreamPost(jsonRequest({ message: "hello" }), {
+      getSession: async () => session({ apiKey: undefined, accessToken: undefined }),
+      apiBaseUrl: "https://example.test",
+      connect: connect as unknown as typeof fetch
+    });
+    expect(res.status).toBe(401);
+    expect(connect).not.toHaveBeenCalled();
+  });
+
+  it("returns 400 when tenant id cannot be resolved", async () => {
+    const connect = vi.fn();
+    const res = await handleCampaignAgentStreamPost(jsonRequest({ message: "hello" }), {
+      getSession: async () => session({ tenantId: "  " }),
+      apiBaseUrl: "https://example.test",
+      connect: connect as unknown as typeof fetch
+    });
+    expect(res.status).toBe(400);
+    expect(connect).not.toHaveBeenCalled();
+  });
+
+  it("returns 200 event-stream, forwards signal, and omits linkedCampaignId", async () => {
+    vi.spyOn(console, "info").mockImplementation(() => {});
+    const abort = new AbortController();
+    const connect = vi.fn(
+      async () =>
+        new Response('event: done\ndata: {"conversationId":"c1"}\n\n', {
+          status: 200,
+          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
+        })
+    );
+    const request = jsonRequest(
+      { message: "  hello  ", conversationId: "conv-1", linkedCampaignId: "camp-secret" },
+      { signal: abort.signal }
+    );
+    const res = await handleCampaignAgentStreamPost(request, {
+      getSession: async () => session(),
+      apiBaseUrl: "https://example.test",
+      connect: connect as unknown as typeof fetch
+    });
+    expect(res.status).toBe(200);
+    expect(res.headers.get("Content-Type")).toBe("text/event-stream; charset=utf-8");
+    expect(res.headers.get("Cache-Control")).toBe("no-cache, no-transform");
+    expect(res.headers.get("Connection")).toBe("keep-alive");
+    expect(res.headers.get("X-Accel-Buffering")).toBe("no");
+    const body = await readResponseText(res);
+    expect(body).toContain("event: done");
+    expect(connect).toHaveBeenCalledTimes(1);
+    const [url, init] = connect.mock.calls[0] as unknown as [string, RequestInit];
+    expect(url).toBe("https://example.test/api/v1/acme/campaign-agent/messages/stream");
+    expect(init.method).toBe("POST");
+    expect(init.signal).toBe(request.signal);
+    const sent = JSON.parse(String(init.body)) as Record<string, unknown>;
+    expect(sent).toEqual({ message: "hello", conversationId: "conv-1" });
+    expect(sent).not.toHaveProperty("linkedCampaignId");
+    expect(res.headers.get("Content-Type")).not.toMatch(/application\/json/);
+  });
+
+  it("logs once when the response body is cancelled before the stream ends", async () => {
+    const info = vi.spyOn(console, "info").mockImplementation(() => {});
+    const connect = vi.fn(
+      async () =>
+        new Response(
+          new ReadableStream({
+            start(controller) {
+              controller.enqueue(new TextEncoder().encode("event: chunk\ndata: {}\n\n"));
+            }
+          }),
+          {
+            status: 200,
+            headers: { "Content-Type": "text/event-stream; charset=utf-8" }
+          }
+        )
+    );
+    const res = await handleCampaignAgentStreamPost(
+      jsonRequest({ message: "do not log this prompt", conversationId: "conv-cancel" }),
+      {
+        getSession: async () => session({ userId: "user-cancel" }),
+        apiBaseUrl: "https://example.test",
+        connect: connect as unknown as typeof fetch
+      }
+    );
+    const reader = res.body!.getReader();
+    await reader.read();
+    await reader.cancel();
+    expect(info).toHaveBeenCalledTimes(1);
+    expect(info).toHaveBeenCalledWith({
+      tenantId: "acme",
+      conversationId: "conv-cancel",
+      userId: "user-cancel",
+      status: 200
+    });
+    const serialized = JSON.stringify(info.mock.calls);
+    expect(serialized).not.toMatch(/do not log this prompt/);
+  });
+
+  it("logs tenantId, conversationId, userId, and status when the stream ends", async () => {
+    const info = vi.spyOn(console, "info").mockImplementation(() => {});
+    const connect = vi.fn(
+      async () =>
+        new Response("event: done\ndata: {}\n\n", {
+          status: 200,
+          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
+        })
+    );
+    const res = await handleCampaignAgentStreamPost(
+      jsonRequest({ message: "do not log this prompt", conversationId: "conv-9" }),
+      {
+        getSession: async () => session({ userId: "user-42", apiKey: "super-secret-key" }),
+        apiBaseUrl: "https://example.test",
+        connect: connect as unknown as typeof fetch
+      }
+    );
+    await readResponseText(res);
+    expect(info).toHaveBeenCalledTimes(1);
+    expect(info).toHaveBeenCalledWith({
+      tenantId: "acme",
+      conversationId: "conv-9",
+      userId: "user-42",
+      status: 200
+    });
+    const serialized = JSON.stringify(info.mock.calls);
+    expect(serialized).not.toMatch(/do not log this prompt/);
+    expect(serialized).not.toMatch(/super-secret-key/);
+    expect(serialized).not.toMatch(/Authorization/i);
+  });
+});
diff --git a/Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts b/Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts
new file mode 100644
index 0000000..9b323fe
--- /dev/null
+++ b/Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts
@@ -0,0 +1,25 @@
+import { auth } from "@/auth";
+import { handleCampaignAgentStreamPost } from "@/lib/campaign-agent/stream-route";
+import type { JourneysSession } from "@/lib/journeys-fetch";
+
+export const dynamic = "force-dynamic";
+export const maxDuration = 600;
+
+async function getSession(): Promise<JourneysSession | null> {
+  const s = await auth();
+  if (!s?.user?.id) return null;
+  return {
+    userId: s.user.id,
+    tenantId: (s as { tenantId?: string }).tenantId ?? "",
+    accessToken: (s as { accessToken?: string }).accessToken,
+    apiKey: (s as { apiKey?: string }).apiKey
+  };
+}
+
+export async function POST(request: Request) {
+  return handleCampaignAgentStreamPost(request, {
+    getSession,
+    apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? "",
+    connect: fetch
+  });
+}
