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

- [ ] **Step 5: Commit** â€” skip unless the user asks.

---