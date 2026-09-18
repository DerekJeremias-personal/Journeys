# Review package Task 5
Base: uncommitted
Head: working tree

diff --git a/Journeys.UX/src/lib/campaign-agent/chat-state.ts b/Journeys.UX/src/lib/campaign-agent/chat-state.ts
new file mode 100644
index 0000000..2fb6b93
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/chat-state.ts
@@ -0,0 +1,64 @@
+import type { SseEvent } from "./sse";
+
+export type ChatLine = { role: "user" | "assistant" | "error"; text: string };
+
+export type AgentChatState = {
+  conversationId: string | null;
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
+    streaming: true
+  };
+}
+
+export function applyAgentSseEvent(state: AgentChatState, ev: SseEvent): AgentChatState {
+  switch (ev.event) {
+    case "started": {
+      const id = parseJson(ev.data)?.conversationId;
+      if (typeof id !== "string" || !id) return state;
+      return { ...state, conversationId: id };
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
+    case "done":
+      return { ...state, streaming: false };
+    case "error": {
+      const message = parseJson(ev.data)?.message;
+      const text = typeof message === "string" ? message : "error";
+      return {
+        ...state,
+        streaming: false,
+        lines: [...state.lines, { role: "error", text }]
+      };
+    }
+    default:
+      return state;
+  }
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts b/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts
new file mode 100644
index 0000000..08bd802
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/chat-state.test.ts
@@ -0,0 +1,32 @@
+import { describe, expect, it } from "vitest";
+import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "./chat-state";
+
+const empty: AgentChatState = { conversationId: null, lines: [], streaming: false };
+
+describe("applyAgentSseEvent", () => {
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
+  it("ignores progress events", () => {
+    const s = applyAgentSseEvent(empty, { event: "progress", data: '{"label":"x"}' });
+    expect(s).toEqual(empty);
+  });
+});
diff --git a/Journeys.UX/src/components/loyalty/agent-chat.tsx b/Journeys.UX/src/components/loyalty/agent-chat.tsx
new file mode 100644
index 0000000..509f22f
--- /dev/null
+++ b/Journeys.UX/src/components/loyalty/agent-chat.tsx
@@ -0,0 +1,115 @@
+"use client";
+
+import { FormEvent, useState } from "react";
+import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "@/lib/campaign-agent/chat-state";
+import { pushSseBytes } from "@/lib/campaign-agent/sse";
+
+const empty: AgentChatState = { conversationId: null, lines: [], streaming: false };
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
+export function AgentChat() {
+  const [input, setInput] = useState("");
+  const [state, setState] = useState<AgentChatState>(empty);
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
+      const res = await fetch("/api/loyalty/campaign-agent/messages/stream", {
+        method: "POST",
+        headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
+        body: JSON.stringify({ message, conversationId: state.conversationId }),
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
+  return (
+    <div className="agent-chat">
+      {state.conversationId ? <small>{state.conversationId}</small> : null}
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
+          placeholder="Describe what you want to build or changeΓÇª"
+        />
+        <button type="submit" disabled={state.streaming}>
+          Send
+        </button>
+      </form>
+    </div>
+  );
+}
diff --git a/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx b/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx
new file mode 100644
index 0000000..7f6ffe3
--- /dev/null
+++ b/Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx
@@ -0,0 +1,10 @@
+import { AgentChat } from "@/components/loyalty/agent-chat";
+
+export default function CampaignAgentPage() {
+  return (
+    <>
+      <h1>Agent</h1>
+      <AgentChat />
+    </>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/app/globals.css b/Journeys/Journeys.UX/src/app/globals.css
index 764c7fb..3324d1a 100644
--- a/Journeys/Journeys.UX/src/app/globals.css
+++ b/Journeys/Journeys.UX/src/app/globals.css
@@ -14,3 +14,5 @@ main { padding: 1.5rem; flex: 1; }
 .empty { color: #555; }
 table { border-collapse: collapse; width: 100%; }
 th, td { border: 1px solid #ddd; padding: 0.4rem 0.6rem; text-align: left; }
+.agent-chat .transcript { display: flex; flex-direction: column; gap: 0.75rem; }
+.agent-chat form { margin-top: 1rem; display: flex; gap: 0.5rem; }
diff --git a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
index 63486bd..2ab8300 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/campaigns/page.tsx
@@ -1,3 +1,4 @@
+import Link from "next/link";
 import { getCampaigns } from "@/services/loyalty/actions";
 
 function formatFetchError(error?: string): string {
@@ -13,7 +14,10 @@ export default async function CampaignsPage() {
 
   return (
     <>
-      <h1>Campaigns</h1>
+      <div>
+        <h1>Campaigns</h1>
+        <p><Link href="/loyalty/campaigns/agent">Agent</Link></p>
+      </div>
       {!result.success ? (
         <p className="error">{formatFetchError(result.error)}</p>
       ) : result.data!.length === 0 ? (
