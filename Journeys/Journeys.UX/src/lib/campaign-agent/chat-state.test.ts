import { describe, expect, it } from "vitest";
import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "./chat-state";

const empty: AgentChatState = {
  conversationId: null,
  progressLabel: null,
  linkedCampaignId: null,
  discoveredCampaignId: null,
  lines: [],
  streaming: false
};

describe("applyAgentSseEvent", () => {
  it("started with both ids sets both; error keeps them", () => {
    let s = applyAgentSseEvent(empty, {
      event: "started",
      data: '{"conversationId":"conv-1","linkedCampaignId":"camp-1"}'
    });
    expect(s.conversationId).toBe("conv-1");
    expect(s.linkedCampaignId).toBe("camp-1");
    s = applyAgentSseEvent(s, { event: "error", data: '{"message":"tool failed"}' });
    expect(s.conversationId).toBe("conv-1");
    expect(s.linkedCampaignId).toBe("camp-1");
    expect(s.streaming).toBe(false);
    expect(s.lines.at(-1)).toEqual({ role: "error", text: "tool failed" });
  });

  it("reads PascalCase ConversationId from started", () => {
    const s = applyAgentSseEvent(empty, {
      event: "started",
      data: '{"ConversationId":"conv-pascal"}'
    });
    expect(s.conversationId).toBe("conv-pascal");
  });

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

  it("stores progress.label without dropping conversationId", () => {
    let s = applyAgentSseEvent(empty, { event: "started", data: '{"conversationId":"c1"}' });
    s = applyAgentSseEvent(s, { event: "progress", data: '{"label":"Thinking…"}' });
    expect(s.conversationId).toBe("c1");
    expect(s.progressLabel).toBe("Thinking…");
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
});
