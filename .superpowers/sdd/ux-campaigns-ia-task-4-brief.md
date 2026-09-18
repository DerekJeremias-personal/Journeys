### Task 4: Agent data plane + inspector + resume

**Files:**
- Create: `Journeys.UX/src/lib/campaign-agent/extract-campaign-id.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/extract-campaign-id.test.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/chat-state.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/chat-state.test.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/stream-route.ts`
- Modify: `Journeys.UX/src/lib/campaign-agent/stream-route.test.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/hydrate-policy.ts`
- Create: `Journeys.UX/src/lib/campaign-agent/hydrate-policy.test.ts`
- Create: `Journeys.UX/src/components/loyalty/campaign-json-disclosure.tsx`
- Modify: `Journeys.UX/src/components/loyalty/agent-chat.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx`
- Modify: `Journeys.UX/src/app/loyalty/campaigns/page.tsx` (resume link)
- Create: `Journeys.UX/src/app/loyalty/campaigns/[id]/agent/page.tsx`

**Interfaces:**
- Consumes: shipped `applyAgentSseEvent`, `handleCampaignAgentStreamPost`
- Produces:
  - `extractCampaignIdFromMcpData(data: string): string | null`
  - `AgentChatState` adds `progressLabel: string | null`, `linkedCampaignId: string | null`, `discoveredCampaignId: string | null`
  - `hydrateDecision(isDirty: boolean): "apply" | "conflict"`
  - `AgentChat` props: `{ linkedCampaignId?: string | null; conversationId?: string | null }`
  - Stream JSON body includes `linkedCampaignId?`, `clientMessageId?`

- [ ] **Step 1: Failing tests**

`extract-campaign-id.ts` â€” lift EXP `findCampaignId` (uuid keys `id` / `campaignId` / `linkedCampaignId` and PascalCase). Test:

```ts
expect(extractCampaignIdFromMcpData(JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })))
  .toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
expect(extractCampaignIdFromMcpData("{}")).toBeNull();
```

Replace `chat-state` â€œignores progressâ€ with:

```ts
it("stores progress.label without dropping conversationId", () => {
  let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
  s = applyAgentSseEvent(s, { event: "progress", data: '{"label":"Thinkingâ€¦"}' });
  expect(s.conversationId).toBe("c1");
  expect(s.progressLabel).toBe("Thinkingâ€¦");
});

it("mcp/workflow set discoveredCampaignId from payload; unknown events leave state", () => {
  let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
  s = applyAgentSseEvent(s, {
    event: "mcp",
    data: JSON.stringify({ campaignId: "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31" })
  });
  expect(s.discoveredCampaignId).toBe("12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31");
  const before = { ...s };
  s = applyAgentSseEvent(s, { event: "nope", data: "{}" });
  expect(s.conversationId).toBe(before.conversationId);
});

it("error keeps conversationId and linkedCampaignId", () => {
  const start: AgentChatState = {
    conversationId: "c1",
    linkedCampaignId: "camp-1",
    discoveredCampaignId: "camp-1",
    progressLabel: "x",
    lines: [],
    streaming: true
  };
  const s = applyAgentSseEvent(start, { event: "error", data: '{"message":"nope"}' });
  expect(s.conversationId).toBe("c1");
  expect(s.linkedCampaignId).toBe("camp-1");
  expect(s.streaming).toBe(false);
});
```

`hydrate-policy.ts`:

```ts
export function hydrateDecision(isDirty: boolean): "apply" | "conflict" {
  return isDirty ? "conflict" : "apply";
}
```

`stream-route.test.ts`: when connect is mocked, forwarded JSON includes `linkedCampaignId` and `clientMessageId` if the request body had them.

- [ ] **Step 2: Run â€” expect FAIL** (progress currently ignored)

- [ ] **Step 3: Implement reducer, extract helper, stream forward**

`StreamBody`:

```ts
type StreamBody = {
  message?: string;
  conversationId?: string | null;
  linkedCampaignId?: string | null;
  clientMessageId?: string | null;
};
```

Forward those fields in `JSON.stringify` to upstream (omit nulls). Generate `clientMessageId` in `AgentChat` per send (`crypto.randomUUID()`).

On `progress`, set `progressLabel` from `label`. On `mcp` / `workflow` / `done` / `tool` / `tool_result`, set `discoveredCampaignId` when extract returns an id. Do **not** parse EXP `campaign_snapshot` as a required event (ignore if present).

- [ ] **Step 4: `AgentChat` + inspector + Live route + resume**

- Props: `linkedCampaignId`, `conversationId` (seed state; reload without query still new thread).
- Show `progressLabel` as one line above the transcript.
- After `discoveredCampaignId` or prop id: render `CampaignJsonDisclosure` â€” `<details>` **without** `open` (collapsed). Summary text `Campaign JSON`. Fetch via `getCampaign(id, "draft")` then without status. Pretty `JSON.stringify(data, null, 2)` + copy button. Errors stay inside the disclosure.
- Do not add JSON inspector modal, tool-activity log, or clear-session.
- `/loyalty/campaigns/[id]/agent/page.tsx`: read `id` + `campaignStatus`. If normalized status !== `live`, redirect to `/loyalty/campaigns/${id}?campaignStatus=` (or campaigns list) â€” do not render chat. If live, `<AgentChat linkedCampaignId={id} />`.
- List resume: `listAgentConversations()`; if `items[0].conversationId`, show â€œResume agentâ€ â†’ `/loyalty/campaigns/agent?conversationId=`. Hide when empty.
- Agent page reads `searchParams.conversationId` and passes it into `AgentChat`.

Hydrate against the builder store is Task 5; this task only exports `hydrateDecision` and updates `discoveredCampaignId` so Task 5 can subscribe.

- [ ] **Step 5: `npm test` â€” expect PASS.** Do not commit unless asked.

---

