### Task 5: Chat state, page, and Campaigns link

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/chat-state.ts`
- Test: `Journeys.UX/src/lib/campaign-agent/chat-state.test.ts`
- Create: `Journeys.UX/src/components/loyalty/agent-chat.tsx`
- Create: `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/page.tsx` â€” add **Agent** link above the list
- Modify: `Journeys.UX/src/app/globals.css` â€” `.agent-chat` transcript + form spacing only

**Interfaces:**
- Consumes: `SseEvent`, `pushSseBytes`; POST `/api/loyalty/campaign-agent/messages/stream`
- Produces:
  - `export type ChatLine = { role: "user" | "assistant" | "error"; text: string }`
  - `export type AgentChatState = { conversationId: string | null; lines: ChatLine[]; streaming: boolean }`
  - `applyAgentSseEvent(state: AgentChatState, ev: SseEvent): AgentChatState`
  - `beginUserTurn(state: AgentChatState, message: string): AgentChatState`
  - Page heading exactly `Agent`. Placeholder: `Describe what you want to build or changeâ€¦`

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

- [ ] **Step 7: Campaigns list link** â€” at the top of `campaigns/page.tsx`, next to the heading:

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

- [ ] **Step 9: Commit** â€” skip unless the user asks.

---