import type { SseEvent } from "./sse";
import { extractCampaignIdFromMcpData } from "./extract-campaign-id";

export type ChatLine = { role: "user" | "assistant" | "error"; text: string };

export type AgentChatState = {
  conversationId: string | null;
  progressLabel: string | null;
  linkedCampaignId: string | null;
  discoveredCampaignId: string | null;
  lines: ChatLine[];
  streaming: boolean;
};

function parseJson(data: string): Record<string, unknown> | null {
  try {
    const value: unknown = JSON.parse(data);
    if (value && typeof value === "object" && !Array.isArray(value)) {
      return value as Record<string, unknown>;
    }
    return null;
  } catch {
    return null;
  }
}

export function beginUserTurn(state: AgentChatState, message: string): AgentChatState {
  return {
    ...state,
    lines: [...state.lines, { role: "user", text: message }],
    streaming: true,
    progressLabel: null
  };
}

export function applyAgentSseEvent(state: AgentChatState, ev: SseEvent): AgentChatState {
  switch (ev.event) {
    case "started": {
      const json = parseJson(ev.data);
      if (!json) return state;
      const conversationId = json.conversationId ?? json.ConversationId;
      const linkedCampaignId = json.linkedCampaignId ?? json.LinkedCampaignId;
      let next = state;
      if (typeof conversationId === "string" && conversationId) {
        next = { ...next, conversationId };
      }
      if (typeof linkedCampaignId === "string" && linkedCampaignId) {
        next = { ...next, linkedCampaignId };
      }
      return next;
    }
    case "progress": {
      const label = parseJson(ev.data)?.label;
      if (typeof label !== "string") return state;
      return { ...state, progressLabel: label };
    }
    case "delta": {
      const text = parseJson(ev.data)?.text;
      if (typeof text !== "string") return state;
      const last = state.lines.at(-1);
      if (last?.role === "assistant") {
        return {
          ...state,
          lines: [...state.lines.slice(0, -1), { role: "assistant", text: last.text + text }]
        };
      }
      return { ...state, lines: [...state.lines, { role: "assistant", text }] };
    }
    case "mcp":
    case "workflow":
    case "tool":
    case "tool_result": {
      const discoveredCampaignId = extractCampaignIdFromMcpData(ev.data);
      if (!discoveredCampaignId) return state;
      return { ...state, discoveredCampaignId };
    }
    case "done": {
      const discoveredCampaignId = extractCampaignIdFromMcpData(ev.data);
      return {
        ...state,
        streaming: false,
        progressLabel: null,
        ...(discoveredCampaignId ? { discoveredCampaignId } : {})
      };
    }
    case "error": {
      const message = parseJson(ev.data)?.message;
      const text = typeof message === "string" ? message : "error";
      return {
        ...state,
        streaming: false,
        progressLabel: null,
        lines: [...state.lines, { role: "error", text }]
      };
    }
    default:
      return state;
  }
}
