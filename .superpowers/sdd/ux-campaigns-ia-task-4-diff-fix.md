# Task 4 review package after fix (working tree; no commit)

## git diff --stat

 .../Journeys.UX/src/app/loyalty/campaigns/page.tsx | 61 +++++++++++++---------  1 file changed, 35 insertions(+), 26 deletions(-)

## untracked

Journeys/Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx
Journeys/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx
Journeys/Journeys.UX/src/components/loyalty/agent-chat.tsx
Journeys/Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx
Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts
Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.ts
Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts
Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts
Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts
Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts
Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts
Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.ts

## Diff

diff --git a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
index 63486bd..54933f4 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
@@ -1,32 +1,41 @@
-import { getCampaigns } from "@/services/loyalty/actions";
-
-function formatFetchError(error?: string): string {
-  const e = error ?? "";
-  if (/401|403|unauthorized|forbidden|nope/i.test(e)) {
-    return "not authorized / check tenant or key";
-  }
-  return e || "Unknown error";
-}
+import Link from "next/link";
+import { CampaignsClient } from "@/components/loyalty/campaigns";
+import { listAgentConversations } from "@/services/loyalty/actions";
 
 export default async function CampaignsPage() {
-  const result = await getCampaigns();
+  const conversations = await listAgentConversations();
+  const conversationId = conversations.success
+    ? conversations.data?.[0]?.conversationId
+    : undefined;
 
   return (
-    <>
-      <h1>Campaigns</h1>
-      {!result.success ? (
-        <p className="error">{formatFetchError(result.error)}</p>
-      ) : result.data!.length === 0 ? (
-        <p className="empty">No campaigns found.</p>
-      ) : (
-        <ul>
-          {result.data!.map((campaign, index) => (
-            <li key={campaign.id ?? index}>
-              {campaign.name} ΓÇö {campaign.status} ΓÇö {campaign.id}
-            </li>
-          ))}
-        </ul>
-      )}
-    </>
+    <div className="space-y-6">
+      <div className="flex items-start justify-between gap-4">
+        <div>
+          <h1 className="text-2xl font-semibold tracking-tight">Campaigns</h1>
+          <p className="mt-1 text-sm text-muted-foreground">
+            Build and manage customer journey campaigns.
+          </p>
+        </div>
+        <div className="flex items-center gap-4">
+          {conversationId ? (
+            <Link
+              href={`/loyalty/campaigns/agent?conversationId=${encodeURIComponent(conversationId)}`}
+              className="text-sm font-medium text-primary underline-offset-4 hover:underline"
+            >
+              Resume agent
+            </Link>
+          ) : null}
+          <Link
+            href="/loyalty/campaigns/agent"
+            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
+          >
+            Agent
+          </Link>
+        </div>
+      </div>
+
+      <CampaignsClient />
+    </div>
   );
 }

### NEW FILE Journeys/Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx

diff --git a/Journeys/Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx
new file mode 100644
index 0000000..ac7962b
--- /dev/null
+++ b/Journeys/Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx
@@ -0,0 +1,32 @@
+import { AgentChat } from "@/components/loyalty/agent-chat";
+import { normalizeCampaignStatus } from "@/lib/campaign-kebab";
+import { redirect } from "next/navigation";
+
+export default async function LiveCampaignAgentPage({
+  params,
+  searchParams
+}: {
+  params: Promise<{ id: string }>;
+  searchParams: Promise<{ campaignStatus?: string | string[] }>;
+}) {
+  const [{ id }, query] = await Promise.all([params, searchParams]);
+  const rawStatus =
+    typeof query.campaignStatus === "string" ? query.campaignStatus : "";
+  const status = normalizeCampaignStatus(rawStatus);
+
+  if (status !== "live") {
+    if (status) {
+      redirect(
+        `/loyalty/campaigns/${encodeURIComponent(id)}?campaignStatus=${encodeURIComponent(status)}`
+      );
+    }
+    redirect("/loyalty/campaigns");
+  }
+
+  return (
+    <>
+      <h1>Agent</h1>
+      <AgentChat linkedCampaignId={id} />
+    </>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx

diff --git a/Journeys/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx
new file mode 100644
index 0000000..85bf9f0
--- /dev/null
+++ b/Journeys/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx
@@ -0,0 +1,18 @@
+import { AgentChat } from "@/components/loyalty/agent-chat";
+
+export default async function CampaignAgentPage({
+  searchParams
+}: {
+  searchParams: Promise<{ conversationId?: string | string[] }>;
+}) {
+  const query = await searchParams;
+  const conversationId =
+    typeof query.conversationId === "string" ? query.conversationId : null;
+
+  return (
+    <>
+      <h1>Agent</h1>
+      <AgentChat conversationId={conversationId} />
+    </>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/agent-chat.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/agent-chat.tsx b/Journeys/Journeys.UX/src/components/loyalty/agent-chat.tsx
new file mode 100644
index 0000000..c18593a
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/agent-chat.tsx
@@ -0,0 +1,185 @@
+"use client";
+
+import { FormEvent, KeyboardEvent, useState } from "react";
+import { CampaignJsonDisclosure } from "@/components/loyalty/campaign-json-disclosure";
+import { shouldSubmitChatOnEnter } from "@/lib/campaign-agent/chat-keys";
+import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "@/lib/campaign-agent/chat-state";
+import { copyText } from "@/lib/campaign-agent/copy-text";
+import { pushSseBytes } from "@/lib/campaign-agent/sse";
+
+const UNAUTHORIZED = "not authorized / check tenant or key";
+
+function httpErrorText(status: number, body: string): string {
+  if (status === 401 || status === 403) return UNAUTHORIZED;
+  try {
+    const parsed = JSON.parse(body) as { error?: string };
+    if (typeof parsed.error === "string" && parsed.error.trim()) return parsed.error;
+  } catch {
+    /* use body or status */
+  }
+  return body.trim() || `Request failed (${status})`;
+}
+
+function applyError(state: AgentChatState, message: string): AgentChatState {
+  return applyAgentSseEvent(state, { event: "error", data: JSON.stringify({ message }) });
+}
+
+function CopyIcon() {
+  return (
+    <svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
+      <rect x="5.5" y="5.5" width="8" height="8" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
+      <path
+        d="M10.5 5.5V3.75A1.25 1.25 0 0 0 9.25 2.5H3.75A1.25 1.25 0 0 0 2.5 3.75v5.5A1.25 1.25 0 0 0 3.75 10.5H5.5"
+        fill="none"
+        stroke="currentColor"
+        strokeWidth="1.5"
+      />
+    </svg>
+  );
+}
+
+function ConversationIdBar({ conversationId }: { conversationId: string }) {
+  const [copied, setCopied] = useState(false);
+
+  async function onCopy() {
+    const ok = await copyText(conversationId);
+    if (!ok) return;
+    setCopied(true);
+    window.setTimeout(() => setCopied(false), 1500);
+  }
+
+  return (
+    <div className="conversation-id">
+      <span className="conversation-id-label">Conversation</span>
+      <code title={conversationId}>{conversationId}</code>
+      <button type="button" onClick={onCopy} aria-label={copied ? "Copied" : "Copy conversation id"} title="Copy">
+        <CopyIcon />
+      </button>
+    </div>
+  );
+}
+
+export function AgentChat({
+  linkedCampaignId = null,
+  conversationId = null
+}: {
+  linkedCampaignId?: string | null;
+  conversationId?: string | null;
+}) {
+  const [input, setInput] = useState("");
+  const [state, setState] = useState<AgentChatState>(() => ({
+    conversationId,
+    progressLabel: null,
+    linkedCampaignId,
+    discoveredCampaignId: null,
+    lines: [],
+    streaming: false
+  }));
+
+  async function onSubmit(event: FormEvent<HTMLFormElement>) {
+    event.preventDefault();
+    const message = input.trim();
+    if (!message || state.streaming) return;
+
+    let next = beginUserTurn(state, message);
+    setState(next);
+    setInput("");
+
+    try {
+      const currentCampaignId = state.discoveredCampaignId ?? state.linkedCampaignId;
+      const res = await fetch("/api/loyalty/campaign-agent/messages/stream", {
+        method: "POST",
+        headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
+        body: JSON.stringify({
+          message,
+          conversationId: state.conversationId,
+          linkedCampaignId: currentCampaignId,
+          clientMessageId: crypto.randomUUID()
+        }),
+        cache: "no-store"
+      });
+
+      const contentType = res.headers.get("content-type") ?? "";
+      const isEventStream = contentType.includes("text/event-stream");
+
+      if (!res.ok && !isEventStream) {
+        next = applyError(next, httpErrorText(res.status, await res.text()));
+        setState(next);
+        return;
+      }
+
+      if (!res.body) {
+        next = applyError(next, "Expected text/event-stream from server.");
+        setState(next);
+        return;
+      }
+
+      const reader = res.body.getReader();
+      const decoder = new TextDecoder();
+      let buf = "";
+
+      while (true) {
+        const { done, value } = await reader.read();
+        if (done) break;
+        const parsed = pushSseBytes(buf, decoder.decode(value, { stream: true }));
+        buf = parsed.buffer;
+        for (const ev of parsed.events) {
+          next = applyAgentSseEvent(next, ev);
+          setState(next);
+        }
+      }
+
+      const flushed = pushSseBytes(buf, "\n\n");
+      for (const ev of flushed.events) {
+        next = applyAgentSseEvent(next, ev);
+        setState(next);
+      }
+
+      if (next.streaming) {
+        next = { ...next, streaming: false };
+        setState(next);
+      }
+    } catch (err) {
+      const text = err instanceof Error ? err.message : String(err);
+      next = applyError(next, text);
+      setState(next);
+    }
+  }
+
+  const inspectorCampaignId = state.discoveredCampaignId ?? state.linkedCampaignId;
+
+  return (
+    <div className="agent-chat">
+      {state.conversationId ? <ConversationIdBar conversationId={state.conversationId} /> : null}
+      {state.progressLabel ? (
+        <p className="text-sm text-muted-foreground">{state.progressLabel}</p>
+      ) : null}
+      <div className="transcript">
+        {state.lines.map((line, index) => (
+          <p key={index} className={line.role === "error" ? "error" : undefined}>
+            {line.text}
+          </p>
+        ))}
+      </div>
+      <form onSubmit={onSubmit}>
+        <textarea
+          value={input}
+          onChange={(e) => setInput(e.target.value)}
+          onKeyDown={(e: KeyboardEvent<HTMLTextAreaElement>) => {
+            if (!shouldSubmitChatOnEnter(e)) return;
+            e.preventDefault();
+            e.currentTarget.form?.requestSubmit();
+          }}
+          placeholder="Describe what you want to build or changeΓÇª"
+          rows={4}
+        />
+        <button type="submit" disabled={state.streaming}>
+          Send
+        </button>
+      </form>
+      {inspectorCampaignId ? (
+        <CampaignJsonDisclosure campaignId={inspectorCampaignId} />
+      ) : null}
+    </div>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx

diff --git a/Journeys/Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx b/Journeys/Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx
new file mode 100644
index 0000000..fc9c1ac
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx
@@ -0,0 +1,66 @@
+"use client";
+
+import { useEffect, useState } from "react";
+import { copyText } from "@/lib/campaign-agent/copy-text";
+import { getCampaign } from "@/services/loyalty/actions";
+
+type CampaignJsonDisclosureProps = {
+  campaignId: string;
+};
+
+export function CampaignJsonDisclosure({ campaignId }: CampaignJsonDisclosureProps) {
+  const [json, setJson] = useState<string | null>(null);
+  const [error, setError] = useState<string | null>(null);
+  const [copied, setCopied] = useState(false);
+
+  useEffect(() => {
+    let cancelled = false;
+
+    async function loadCampaign() {
+      setJson(null);
+      setError(null);
+      const draft = await getCampaign(campaignId, "draft");
+      const result = draft.success && draft.data ? draft : await getCampaign(campaignId);
+      if (cancelled) return;
+      if (!result.success || !result.data) {
+        setError(result.error ?? "Failed to load campaign JSON");
+        return;
+      }
+      setJson(JSON.stringify(result.data, null, 2));
+    }
+
+    void loadCampaign().catch((reason: unknown) => {
+      if (!cancelled) {
+        setError(reason instanceof Error ? reason.message : "Failed to load campaign JSON");
+      }
+    });
+
+    return () => {
+      cancelled = true;
+    };
+  }, [campaignId]);
+
+  async function onCopy() {
+    if (!json || !(await copyText(json))) return;
+    setCopied(true);
+    window.setTimeout(() => setCopied(false), 1500);
+  }
+
+  return (
+    <details className="rounded-md border border-border p-3">
+      <summary className="cursor-pointer font-medium">Campaign JSON</summary>
+      <div className="mt-3">
+        {error ? <p className="error">{error}</p> : null}
+        {!error && !json ? <p className="text-sm text-muted-foreground">LoadingΓÇª</p> : null}
+        {json ? (
+          <>
+            <button type="button" className="mb-2 text-sm underline" onClick={onCopy}>
+              {copied ? "Copied" : "Copy"}
+            </button>
+            <pre className="max-h-96 overflow-auto rounded bg-muted p-3 text-xs">{json}</pre>
+          </>
+        ) : null}
+      </div>
+    </details>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts
new file mode 100644
index 0000000..d0c7a66
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts
@@ -0,0 +1,82 @@
+import { describe, expect, it } from "vitest";
+import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "./chat-state";
+
+const empty: AgentChatState = {
+  conversationId: null,
+  progressLabel: null,
+  linkedCampaignId: null,
+  discoveredCampaignId: null,
+  lines: [],
+  streaming: false
+};
+
+describe("applyAgentSseEvent", () => {
+  it("started with both ids sets both; error keeps them", () => {
+    let s = applyAgentSseEvent(empty, {
+      event: "started",
+      data: '{"conversationId":"conv-1","linkedCampaignId":"camp-1"}'
+    });
+    expect(s.conversationId).toBe("conv-1");
+    expect(s.linkedCampaignId).toBe("camp-1");
+    s = applyAgentSseEvent(s, { event: "error", data: '{"message":"tool failed"}' });
+    expect(s.conversationId).toBe("conv-1");
+    expect(s.linkedCampaignId).toBe("camp-1");
+    expect(s.streaming).toBe(false);
+    expect(s.lines.at(-1)).toEqual({ role: "error", text: "tool failed" });
+  });
+
+  it("stores conversationId from started and keeps it on error", () => {
+    let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"conv-1"}' });
+    expect(s.conversationId).toBe("conv-1");
+    s = applyAgentSseEvent(s, { event: "error", data: '{"message":"tool failed"}' });
+    expect(s.conversationId).toBe("conv-1");
+    expect(s.streaming).toBe(false);
+    expect(s.lines.at(-1)).toEqual({ role: "error", text: "tool failed" });
+  });
+
+  it("appends delta text onto one assistant line", () => {
+    let s = beginUserTurn(empty, "hi");
+    s = applyAgentSseEvent(s, { event: "delta", data: '{"text":"Hel"}' });
+    s = applyAgentSseEvent(s, { event: "delta", data: '{"text":"lo"}' });
+    s = applyAgentSseEvent(s, { event: "done", data: '{"conversationId":"c"}' });
+    expect(s.lines).toEqual([
+      { role: "user", text: "hi" },
+      { role: "assistant", text: "Hello" }
+    ]);
+    expect(s.streaming).toBe(false);
+  });
+
+  it("stores progress.label without dropping conversationId", () => {
+    let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
+    s = applyAgentSseEvent(s, { event: "progress", data: '{"label":"ThinkingΓÇª"}' });
+    expect(s.conversationId).toBe("c1");
+    expect(s.progressLabel).toBe("ThinkingΓÇª");
+  });
+
+  it("mcp/workflow set discoveredCampaignId from payload; unknown events leave state", () => {
+    let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
+    s = applyAgentSseEvent(s, {
+      event: "mcp",
+      data: JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })
+    });
+    expect(s.discoveredCampaignId).toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
+    const before = { ...s };
+    s = applyAgentSseEvent(s, { event: "nope", data: "{}" });
+    expect(s.conversationId).toBe(before.conversationId);
+  });
+
+  it("error keeps conversationId and linkedCampaignId", () => {
+    const start: AgentChatState = {
+      conversationId: "c1",
+      linkedCampaignId: "camp-1",
+      discoveredCampaignId: "camp-1",
+      progressLabel: "x",
+      lines: [],
+      streaming: true
+    };
+    const s = applyAgentSseEvent(start, { event: "error", data: '{"message":"nope"}' });
+    expect(s.conversationId).toBe("c1");
+    expect(s.linkedCampaignId).toBe("camp-1");
+    expect(s.streaming).toBe(false);
+  });
+});

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.ts
new file mode 100644
index 0000000..02bd9af
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/chat-state.ts
@@ -0,0 +1,99 @@
+import type { SseEvent } from "./sse";
+import { extractCampaignIdFromMcpData } from "./extract-campaign-id";
+
+export type ChatLine = { role: "user" | "assistant" | "error"; text: string };
+
+export type AgentChatState = {
+  conversationId: string | null;
+  progressLabel: string | null;
+  linkedCampaignId: string | null;
+  discoveredCampaignId: string | null;
+  lines: ChatLine[];
+  streaming: boolean;
+};
+
+function parseJson(data: string): Record<string, unknown> | null {
+  try {
+    const value: unknown = JSON.parse(data);
+    if (value && typeof value === "object" && !Array.isArray(value)) {
+      return value as Record<string, unknown>;
+    }
+    return null;
+  } catch {
+    return null;
+  }
+}
+
+export function beginUserTurn(state: AgentChatState, message: string): AgentChatState {
+  return {
+    ...state,
+    lines: [...state.lines, { role: "user", text: message }],
+    streaming: true,
+    progressLabel: null
+  };
+}
+
+export function applyAgentSseEvent(state: AgentChatState, ev: SseEvent): AgentChatState {
+  switch (ev.event) {
+    case "started": {
+      const json = parseJson(ev.data);
+      if (!json) return state;
+      const conversationId = json.conversationId;
+      const linkedCampaignId = json.linkedCampaignId ?? json.LinkedCampaignId;
+      let next = state;
+      if (typeof conversationId === "string" && conversationId) {
+        next = { ...next, conversationId };
+      }
+      if (typeof linkedCampaignId === "string" && linkedCampaignId) {
+        next = { ...next, linkedCampaignId };
+      }
+      return next;
+    }
+    case "progress": {
+      const label = parseJson(ev.data)?.label;
+      if (typeof label !== "string") return state;
+      return { ...state, progressLabel: label };
+    }
+    case "delta": {
+      const text = parseJson(ev.data)?.text;
+      if (typeof text !== "string") return state;
+      const last = state.lines.at(-1);
+      if (last?.role === "assistant") {
+        return {
+          ...state,
+          lines: [...state.lines.slice(0, -1), { role: "assistant", text: last.text + text }]
+        };
+      }
+      return { ...state, lines: [...state.lines, { role: "assistant", text }] };
+    }
+    case "mcp":
+    case "workflow":
+    case "tool":
+    case "tool_result": {
+      const discoveredCampaignId = extractCampaignIdFromMcpData(ev.data);
+      if (!discoveredCampaignId) return state;
+      return { ...state, discoveredCampaignId };
+    }
+    case "done": {
+      const discoveredCampaignId = extractCampaignIdFromMcpData(ev.data);
+      return {
+        ...state,
+        streaming: false,
+        progressLabel: null,
+        ...(discoveredCampaignId ? { discoveredCampaignId } : {})
+      };
+    }
+    case "error": {
+      const message = parseJson(ev.data)?.message;
+      const text = typeof message === "string" ? message : "error";
+      return {
+        ...state,
+        streaming: false,
+        progressLabel: null,
+        lines: [...state.lines, { role: "error", text }]
+      };
+    }
+    default:
+      return state;
+  }
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts
new file mode 100644
index 0000000..bb20771
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts
@@ -0,0 +1,21 @@
+import { describe, expect, it } from "vitest";
+import { extractCampaignIdFromMcpData } from "./extract-campaign-id";
+
+describe("extractCampaignIdFromMcpData", () => {
+  it("extracts a campaign UUID", () => {
+    expect(
+      extractCampaignIdFromMcpData(
+        JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })
+      )
+    ).toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
+  });
+
+  it("returns null when no campaign id exists", () => {
+    expect(extractCampaignIdFromMcpData("{}")).toBeNull();
+  });
+
+  it("returns null for invalid JSON and non-UUID ids", () => {
+    expect(extractCampaignIdFromMcpData("{")).toBeNull();
+    expect(extractCampaignIdFromMcpData('{"campaignId":"campaign-1"}')).toBeNull();
+  });
+});

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts
new file mode 100644
index 0000000..b9ae00f
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts
@@ -0,0 +1,39 @@
+const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
+
+function findCampaignId(value: unknown, depth = 0): string | null {
+  if (depth > 8 || value === null || value === undefined || typeof value !== "object") {
+    return null;
+  }
+
+  if (!Array.isArray(value)) {
+    const object = value as Record<string, unknown>;
+    for (const key of [
+      "id",
+      "Id",
+      "campaignId",
+      "CampaignId",
+      "linkedCampaignId",
+      "LinkedCampaignId"
+    ] as const) {
+      const candidate = object[key];
+      if (typeof candidate === "string" && UUID_RE.test(candidate.trim())) {
+        return candidate.trim();
+      }
+    }
+  }
+
+  const items = Array.isArray(value) ? value : Object.values(value);
+  for (const item of items) {
+    const found = findCampaignId(item, depth + 1);
+    if (found) return found;
+  }
+  return null;
+}
+
+export function extractCampaignIdFromMcpData(data: string): string | null {
+  try {
+    return findCampaignId(JSON.parse(data) as unknown);
+  } catch {
+    return null;
+  }
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts
new file mode 100644
index 0000000..cafbbda
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts
@@ -0,0 +1,12 @@
+import { describe, expect, it } from "vitest";
+import { hydrateDecision } from "./hydrate-policy";
+
+describe("hydrateDecision", () => {
+  it("applies to a clean builder", () => {
+    expect(hydrateDecision(false)).toBe("apply");
+  });
+
+  it("reports a conflict for a dirty builder", () => {
+    expect(hydrateDecision(true)).toBe("conflict");
+  });
+});

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts
new file mode 100644
index 0000000..6e057f5
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts
@@ -0,0 +1,3 @@
+export function hydrateDecision(isDirty: boolean): "apply" | "conflict" {
+  return isDirty ? "conflict" : "apply";
+}

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts
new file mode 100644
index 0000000..671d3ab
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.test.ts
@@ -0,0 +1,243 @@
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
+  it("returns 200 event-stream, forwards signal, linkedCampaignId, and clientMessageId", async () => {
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
+      {
+        message: "  hello  ",
+        conversationId: "conv-1",
+        linkedCampaignId: "camp-1",
+        clientMessageId: "message-1"
+      },
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
+    expect(sent).toEqual({
+      message: "hello",
+      conversationId: "conv-1",
+      linkedCampaignId: "camp-1",
+      clientMessageId: "message-1"
+    });
+    expect(res.headers.get("Content-Type")).not.toMatch(/application\/json/);
+  });
+
+  it("omits nullable stream identifiers", async () => {
+    vi.spyOn(console, "info").mockImplementation(() => {});
+    const connect = vi.fn(
+      async () =>
+        new Response("event: done\ndata: {}\n\n", {
+          status: 200,
+          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
+        })
+    );
+    const res = await handleCampaignAgentStreamPost(
+      jsonRequest({
+        message: "hello",
+        conversationId: null,
+        linkedCampaignId: null,
+        clientMessageId: null
+      }),
+      {
+        getSession: async () => session(),
+        apiBaseUrl: "https://example.test",
+        connect: connect as unknown as typeof fetch
+      }
+    );
+    await readResponseText(res);
+    const [, init] = connect.mock.calls[0] as unknown as [string, RequestInit];
+    expect(JSON.parse(String(init.body))).toEqual({ message: "hello" });
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

### NEW FILE Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.ts

diff --git a/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.ts b/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.ts
new file mode 100644
index 0000000..c77133a
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/campaign-agent/stream-route.ts
@@ -0,0 +1,144 @@
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
+  linkedCampaignId?: string | null;
+  clientMessageId?: string | null;
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
+  const reader = stream
+    .pipeThrough(
+      new TransformStream<Uint8Array, Uint8Array>({
+        flush() {
+          logOnce();
+        }
+      })
+    )
+    .getReader();
+
+  return new ReadableStream<Uint8Array>({
+    async pull(controller) {
+      const { done, value } = await reader.read();
+      if (done) {
+        controller.close();
+      } else {
+        controller.enqueue(value);
+      }
+    },
+    cancel(reason) {
+      logOnce();
+      return reader.cancel(reason);
+    }
+  });
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
+  const linkedCampaignId = body.linkedCampaignId || undefined;
+  const clientMessageId = body.clientMessageId || undefined;
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
+        ...(conversationId ? { conversationId } : {}),
+        ...(linkedCampaignId ? { linkedCampaignId } : {}),
+        ...(clientMessageId ? { clientMessageId } : {})
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
