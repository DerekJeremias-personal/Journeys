"use client";

import { FormEvent, KeyboardEvent, useEffect, useRef, useState } from "react";
import { CampaignJsonDisclosure } from "@/components/loyalty/campaign-json-disclosure";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { shouldSubmitChatOnEnter } from "@/lib/campaign-agent/chat-keys";
import { applyAgentSseEvent, beginUserTurn, type AgentChatState } from "@/lib/campaign-agent/chat-state";
import { copyText } from "@/lib/campaign-agent/copy-text";
import { pushSseBytes } from "@/lib/campaign-agent/sse";

const UNAUTHORIZED = "not authorized / check tenant or key";

function httpErrorText(status: number, body: string): string {
  if (status === 401 || status === 403) return UNAUTHORIZED;
  try {
    const parsed = JSON.parse(body) as { error?: string };
    if (typeof parsed.error === "string" && parsed.error.trim()) return parsed.error;
  } catch {
    /* use body or status */
  }
  return body.trim() || `Request failed (${status})`;
}

function applyError(state: AgentChatState, message: string): AgentChatState {
  return applyAgentSseEvent(state, { event: "error", data: JSON.stringify({ message }) });
}

function CopyIcon() {
  return (
    <svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">
      <rect x="5.5" y="5.5" width="8" height="8" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
      <path
        d="M10.5 5.5V3.75A1.25 1.25 0 0 0 9.25 2.5H3.75A1.25 1.25 0 0 0 2.5 3.75v5.5A1.25 1.25 0 0 0 3.75 10.5H5.5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
      />
    </svg>
  );
}

function ConversationIdBar({ conversationId }: { conversationId: string | null }) {
  const [copied, setCopied] = useState(false);

  async function onCopy() {
    if (!conversationId) return;
    const ok = await copyText(conversationId);
    if (!ok) return;
    setCopied(true);
    window.setTimeout(() => setCopied(false), 1500);
  }

  return (
    <div className="conversation-id">
      <span className="conversation-id-label">Conversation</span>
      {conversationId ? (
        <>
          <code title={conversationId}>{conversationId}</code>
          <button type="button" onClick={onCopy} aria-label={copied ? "Copied" : "Copy conversation id"} title="Copy">
            <CopyIcon />
          </button>
        </>
      ) : (
        <span className="conversation-id-pending">
          A conversation id is assigned when you send the first message.
        </span>
      )}
    </div>
  );
}

export function AgentChat({
  linkedCampaignId = null,
  conversationId = null,
  onDiscoveredCampaignId
}: {
  linkedCampaignId?: string | null;
  conversationId?: string | null;
  onDiscoveredCampaignId?: (id: string) => void;
}) {
  const [input, setInput] = useState("");
  const lastReportedCampaignId = useRef<string | null>(null);
  const [state, setState] = useState<AgentChatState>(() => ({
    conversationId,
    progressLabel: null,
    linkedCampaignId,
    discoveredCampaignId: null,
    lines: [],
    streaming: false
  }));

  useEffect(() => {
    const discovered = state.discoveredCampaignId;
    if (!discovered || discovered === lastReportedCampaignId.current) return;
    lastReportedCampaignId.current = discovered;
    onDiscoveredCampaignId?.(discovered);
  }, [onDiscoveredCampaignId, state.discoveredCampaignId]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const message = input.trim();
    if (!message || state.streaming) return;

    let next = beginUserTurn(state, message);
    setState(next);
    setInput("");

    try {
      const currentCampaignId = state.discoveredCampaignId ?? state.linkedCampaignId;
      const res = await fetch("/api/loyalty/campaign-agent/messages/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
        body: JSON.stringify({
          message,
          conversationId: state.conversationId,
          linkedCampaignId: currentCampaignId,
          clientMessageId: crypto.randomUUID()
        }),
        cache: "no-store"
      });

      const contentType = res.headers.get("content-type") ?? "";
      const isEventStream = contentType.includes("text/event-stream");

      if (!res.ok && !isEventStream) {
        next = applyError(next, httpErrorText(res.status, await res.text()));
        setState(next);
        return;
      }

      if (!res.body) {
        next = applyError(next, "Expected text/event-stream from server.");
        setState(next);
        return;
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buf = "";

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const parsed = pushSseBytes(buf, decoder.decode(value, { stream: true }));
        buf = parsed.buffer;
        for (const ev of parsed.events) {
          next = applyAgentSseEvent(next, ev);
          setState(next);
        }
      }

      const flushed = pushSseBytes(buf, "\n\n");
      for (const ev of flushed.events) {
        next = applyAgentSseEvent(next, ev);
        setState(next);
      }

      if (next.streaming) {
        next = { ...next, streaming: false };
        setState(next);
      }
    } catch (err) {
      const text = err instanceof Error ? err.message : String(err);
      next = applyError(next, text);
      setState(next);
    }
  }

  const inspectorCampaignId = state.discoveredCampaignId ?? state.linkedCampaignId;

  return (
    <div className="agent-chat">
      <ConversationIdBar conversationId={state.conversationId} />
      {state.progressLabel ? (
        <p className="text-sm text-muted-foreground">{state.progressLabel}</p>
      ) : null}
      <div className="transcript">
        {state.lines.map((line, index) => (
          <p key={index} className={line.role === "error" ? "error" : undefined}>
            {line.text}
          </p>
        ))}
      </div>
      <form onSubmit={onSubmit}>
        <Textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e: KeyboardEvent<HTMLTextAreaElement>) => {
            if (!shouldSubmitChatOnEnter(e)) return;
            e.preventDefault();
            e.currentTarget.form?.requestSubmit();
          }}
          placeholder="Describe what you want to build or change…"
          rows={4}
          disabled={state.streaming}
        />
        <Button type="submit" disabled={state.streaming}>
          Send
        </Button>
      </form>
      {inspectorCampaignId ? (
        <CampaignJsonDisclosure campaignId={inspectorCampaignId} />
      ) : null}
    </div>
  );
}
