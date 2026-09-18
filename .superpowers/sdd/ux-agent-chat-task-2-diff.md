# Review package Task 2
Base: uncommitted
Head: working tree

diff --git a/Journeys.UX/src/lib/campaign-agent/sse.ts b/Journeys.UX/src/lib/campaign-agent/sse.ts
new file mode 100644
index 0000000..7ae2303
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/sse.ts
@@ -0,0 +1,25 @@
+export type SseEvent = { event: string; data: string };
+
+/**
+ * Append incoming bytes to a buffer and return complete SSE event blocks.
+ */
+export function pushSseBytes(buffer: string, chunk: string): { buffer: string; events: SseEvent[] } {
+  buffer += chunk;
+  const events: SseEvent[] = [];
+  const blocks = buffer.split(/\r?\n\r?\n/);
+  buffer = blocks.pop() ?? "";
+
+  for (const block of blocks) {
+    if (!block.trim()) continue;
+    let eventName = "message";
+    const dataLines: string[] = [];
+    for (const line of block.split(/\r?\n/)) {
+      if (line.startsWith("event:")) eventName = line.slice(6).trim();
+      else if (line.startsWith("data:")) dataLines.push(line.slice(5).trimStart());
+    }
+    const data = dataLines.join("\n");
+    if (data.length > 0) events.push({ event: eventName, data });
+  }
+
+  return { buffer, events };
+}
diff --git a/Journeys.UX/src/lib/campaign-agent/sse.test.ts b/Journeys.UX/src/lib/campaign-agent/sse.test.ts
new file mode 100644
index 0000000..655fc53
--- /dev/null
+++ b/Journeys.UX/src/lib/campaign-agent/sse.test.ts
@@ -0,0 +1,25 @@
+import { describe, expect, it } from "vitest";
+import { pushSseBytes } from "./sse";
+
+describe("pushSseBytes", () => {
+  it("parses started, delta, done, and error blocks", () => {
+    const chunk =
+      'event: started\ndata: {"conversationId":"c1"}\n\n' +
+      'event: delta\ndata: {"text":"Hi"}\n\n' +
+      'event: done\ndata: {"conversationId":"c1"}\n\n' +
+      'event: error\ndata: {"message":"boom"}\n\n';
+    const { buffer, events } = pushSseBytes("", chunk);
+    expect(buffer).toBe("");
+    expect(events.map((e) => e.event)).toEqual(["started", "delta", "done", "error"]);
+    expect(events[0].data).toContain("c1");
+    expect(events[1].data).toContain("Hi");
+    expect(events[3].data).toContain("boom");
+  });
+
+  it("holds a partial block in the buffer", () => {
+    const first = pushSseBytes("", "event: delta\ndata: {\"text\":");
+    expect(first.events).toEqual([]);
+    const second = pushSseBytes(first.buffer, "\"x\"}\n\n");
+    expect(second.events).toEqual([{ event: "delta", data: '{"text":"x"}' }]);
+  });
+});
