# Review package Task 3
Base: uncommitted
Head: working tree

diff --git a/Journeys.UX/src/lib/campaign-agent/proxy-auth.ts b/Journeys.UX/src/lib/campaign-agent/proxy-auth.ts
new file mode 100644
index 0000000..694b5eb
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/proxy-auth.ts
@@ -0,0 +1,23 @@
+import type { JourneysSession } from "../journeys-fetch";
+
+export type CampaignAgentProxyAuth =
+  | { ok: true; session: JourneysSession }
+  | { ok: false; status: 401; error: string };
+
+export function authorizeCampaignAgentProxy(session: JourneysSession | null): CampaignAgentProxyAuth {
+  if (!session?.userId) return { ok: false, status: 401, error: "Not authenticated" };
+  if (!session.accessToken && !session.apiKey) {
+    return { ok: false, status: 401, error: "No credentials in session" };
+  }
+  return { ok: true, session };
+}
+
+export function campaignAgentUpstreamHeaders(session: JourneysSession): Record<string, string> {
+  const headers: Record<string, string> = {
+    Accept: "text/event-stream",
+    "Content-Type": "application/json"
+  };
+  if (session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`;
+  else if (session.apiKey) headers["Journeys-API-KEY"] = session.apiKey;
+  return headers;
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/proxy-auth.test.ts b/Journeys.UX/src/lib/campaign-agent/proxy-auth.test.ts
new file mode 100644
index 0000000..1e75d6e
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/proxy-auth.test.ts
@@ -0,0 +1,40 @@
+import { describe, expect, it } from "vitest";
+import { authorizeCampaignAgentProxy, campaignAgentUpstreamHeaders } from "./proxy-auth";
+import type { JourneysSession } from "../journeys-fetch";
+
+function session(partial: Partial<JourneysSession> = {}): JourneysSession {
+  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
+}
+
+describe("authorizeCampaignAgentProxy", () => {
+  it("rejects missing session without calling upstream", () => {
+    const r = authorizeCampaignAgentProxy(null);
+    expect(r.ok).toBe(false);
+    if (!r.ok) expect(r.status).toBe(401);
+  });
+
+  it("rejects session with no credentials", () => {
+    const r = authorizeCampaignAgentProxy(session({ apiKey: undefined, accessToken: undefined }));
+    expect(r.ok).toBe(false);
+  });
+
+  it("accepts API-key session", () => {
+    const r = authorizeCampaignAgentProxy(session());
+    expect(r.ok).toBe(true);
+  });
+});
+
+describe("campaignAgentUpstreamHeaders", () => {
+  it("sends Journeys-API-KEY when there is no access token", () => {
+    const h = campaignAgentUpstreamHeaders(session({ apiKey: "secret-key", accessToken: undefined }));
+    expect(h["Journeys-API-KEY"]).toBe("secret-key");
+    expect(h.Authorization).toBeUndefined();
+    expect(h.Accept).toBe("text/event-stream");
+  });
+
+  it("sends Bearer and omits API key when accessToken is present", () => {
+    const h = campaignAgentUpstreamHeaders(session({ accessToken: "tok", apiKey: "should-not-send" }));
+    expect(h.Authorization).toBe("Bearer tok");
+    expect(h["Journeys-API-KEY"]).toBeUndefined();
+  });
+});
diff --git a/Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts b/Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts
new file mode 100644
index 0000000..cbf51d9
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts
@@ -0,0 +1,73 @@
+const DEFAULT_IDLE_MS = 15_000;
+const DEFAULT_COMMENT = ": keep-alive";
+
+export type SseKeepAliveOptions = {
+  idleMs?: number;
+  comment?: string;
+};
+
+/**
+ * Wraps an upstream SSE byte stream and emits an SSE comment heartbeat when
+ * no upstream bytes arrive within idleMs. Keeps App Gateway / proxies from
+ * closing idle connections during long MCP tool phases.
+ */
+export function wrapStreamWithSseKeepAlive(
+  upstream: ReadableStream<Uint8Array>,
+  options: SseKeepAliveOptions = {}
+): ReadableStream<Uint8Array> {
+  const idleMs = options.idleMs ?? DEFAULT_IDLE_MS;
+  const encoder = new TextEncoder();
+  const keepAliveBytes = encoder.encode(`${options.comment ?? DEFAULT_COMMENT}\n\n`);
+  const reader = upstream.getReader();
+
+  let idleTimer: ReturnType<typeof setTimeout> | undefined;
+
+  const clearIdleTimer = (): void => {
+    if (idleTimer !== undefined) {
+      clearTimeout(idleTimer);
+      idleTimer = undefined;
+    }
+  };
+
+  return new ReadableStream<Uint8Array>({
+    start(controller) {
+      const scheduleKeepAlive = (): void => {
+        clearIdleTimer();
+        idleTimer = setTimeout(() => {
+          try {
+            controller.enqueue(keepAliveBytes);
+            scheduleKeepAlive();
+          } catch {
+            clearIdleTimer();
+          }
+        }, idleMs);
+      };
+
+      const pump = async (): Promise<void> => {
+        scheduleKeepAlive();
+        try {
+          while (true) {
+            const { done, value } = await reader.read();
+            clearIdleTimer();
+            if (done) {
+              controller.close();
+              return;
+            }
+            controller.enqueue(value);
+            scheduleKeepAlive();
+          }
+        } catch (error) {
+          controller.error(error);
+        } finally {
+          clearIdleTimer();
+        }
+      };
+
+      void pump();
+    },
+    cancel(reason) {
+      clearIdleTimer();
+      return reader.cancel(reason);
+    },
+  });
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts b/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts
new file mode 100644
index 0000000..143b9b3
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts
@@ -0,0 +1,87 @@
+import { allowInsecureLocalHttps, describeFetchFailure } from "../local-https";
+import { wrapStreamWithSseKeepAlive } from "./sse-keepalive";
+
+export type EarlySseProxyOptions = {
+  upstreamUrl: string;
+  upstreamInit: RequestInit;
+  idleMs?: number;
+  signal?: AbortSignal;
+  connect?: typeof fetch;
+};
+
+const MAX_ERROR_BODY_CHARS = 500;
+
+function encodeSseError(message: string): Uint8Array {
+  const encoder = new TextEncoder();
+  return encoder.encode(`event: error\ndata: ${JSON.stringify({ message })}\n\n`);
+}
+
+function httpErrorMessage(status: number, body: string): string {
+  if (status === 401 || status === 403) return "not authorized / check tenant or key";
+  const truncated = body.slice(0, MAX_ERROR_BODY_CHARS);
+  return truncated || `Upstream HTTP ${status}`;
+}
+
+export function createEarlySseProxyStream(options: EarlySseProxyOptions): ReadableStream<Uint8Array> {
+  const idleMs = options.idleMs ?? 15_000;
+  const connect = options.connect ?? fetch;
+
+  const inner = new ReadableStream<Uint8Array>({
+    start(controller) {
+      void (async () => {
+        try {
+          allowInsecureLocalHttps(options.upstreamUrl);
+          let response: Response;
+          try {
+            response = await connect(options.upstreamUrl, {
+              ...options.upstreamInit,
+              signal: options.signal ?? options.upstreamInit.signal
+            });
+          } catch (error) {
+            controller.enqueue(encodeSseError(`Cannot reach Journeys.API (${describeFetchFailure(error)})`));
+            controller.close();
+            return;
+          }
+
+          if (response.status < 200 || response.status >= 300) {
+            const body = await response.text();
+            controller.enqueue(encodeSseError(httpErrorMessage(response.status, body)));
+            controller.close();
+            return;
+          }
+
+          const upstreamBody = response.body;
+          if (!upstreamBody) {
+            controller.close();
+            return;
+          }
+
+          const reader = upstreamBody.getReader();
+          while (true) {
+            const { done, value } = await reader.read();
+            if (done) break;
+            if (value) controller.enqueue(value);
+          }
+          controller.close();
+        } catch (error) {
+          if ((error as Error).name === "AbortError") {
+            try {
+              controller.close();
+            } catch {
+              /* already closed */
+            }
+            return;
+          }
+          try {
+            controller.enqueue(encodeSseError(`Cannot reach Journeys.API (${describeFetchFailure(error)})`));
+            controller.close();
+          } catch {
+            /* already closed */
+          }
+        }
+      })();
+    }
+  });
+
+  return wrapStreamWithSseKeepAlive(inner, { idleMs });
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.test.ts b/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.test.ts
new file mode 100644
index 0000000..5acfdf0
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/sse-early-proxy.test.ts
@@ -0,0 +1,64 @@
+import { describe, expect, it, vi } from "vitest";
+import { createEarlySseProxyStream } from "./sse-early-proxy";
+import { pushSseBytes } from "./sse";
+
+async function readAll(stream: ReadableStream<Uint8Array>): Promise<string> {
+  const reader = stream.getReader();
+  const decoder = new TextDecoder();
+  let out = "";
+  while (true) {
+    const { done, value } = await reader.read();
+    if (done) break;
+    if (value) out += decoder.decode(value, { stream: true });
+  }
+  return out;
+}
+
+describe("createEarlySseProxyStream", () => {
+  it("maps 401 to an SSE error and does not throw", async () => {
+    const connect = vi.fn(async () => new Response(JSON.stringify({ error: "nope" }), { status: 401 }));
+    const stream = createEarlySseProxyStream({
+      upstreamUrl: "https://example.test/stream",
+      upstreamInit: { method: "POST", body: "{}" },
+      connect: connect as unknown as typeof fetch,
+      idleMs: 60_000
+    });
+    const text = await readAll(stream);
+    const { events } = pushSseBytes("", text);
+    const err = events.find((e) => e.event === "error");
+    expect(err).toBeTruthy();
+    expect(err!.data.toLowerCase()).toMatch(/not authorized|nope|401/);
+  });
+
+  it("maps connect failure to Cannot reach Journeys.API", async () => {
+    const connect = vi.fn(async () => {
+      throw new Error("fetch failed");
+    });
+    const stream = createEarlySseProxyStream({
+      upstreamUrl: "https://example.test/stream",
+      upstreamInit: { method: "POST", body: "{}" },
+      connect: connect as unknown as typeof fetch,
+      idleMs: 60_000
+    });
+    const text = await readAll(stream);
+    expect(text).toMatch(/Cannot reach Journeys\.API/);
+    expect(text).not.toMatch(/Coach/i);
+  });
+
+  it("forwards upstream SSE bytes on 200 event-stream", async () => {
+    const body = "event: started\ndata: {\"conversationId\":\"c1\"}\n\n";
+    const connect = vi.fn(
+      async () =>
+        new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream; charset=utf-8" } })
+    );
+    const stream = createEarlySseProxyStream({
+      upstreamUrl: "https://example.test/stream",
+      upstreamInit: { method: "POST", body: "{}" },
+      connect: connect as unknown as typeof fetch,
+      idleMs: 60_000
+    });
+    const text = await readAll(stream);
+    expect(text).toContain("event: started");
+    expect(text).toContain("c1");
+  });
+});
