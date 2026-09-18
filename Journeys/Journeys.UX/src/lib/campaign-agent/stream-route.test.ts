import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { JourneysSession } from "../journeys-fetch";
import { handleCampaignAgentStreamPost } from "./stream-route";

function session(partial: Partial<JourneysSession> = {}): JourneysSession {
  return { userId: "u1", tenantId: "acme", apiKey: "k", ...partial };
}

function jsonRequest(body: unknown, init: RequestInit = {}): Request {
  return new Request("http://localhost/api/loyalty/campaign-agent/messages/stream", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    ...init
  });
}

async function readResponseText(res: Response): Promise<string> {
  return res.text();
}

describe("handleCampaignAgentStreamPost", () => {
  beforeEach(() => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  it("returns 401 and does not call connect when session is missing", async () => {
    const connect = vi.fn();
    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/api/loyalty/campaign-agent/messages/stream", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: "hello" })
    }), {
      getSession: async () => null,
      apiBaseUrl: "https://example.test",
      connect: connect as unknown as typeof fetch
    });
    expect(res.status).toBe(401);
    expect(connect).not.toHaveBeenCalled();
    await expect(res.json()).resolves.toMatchObject({ error: expect.stringMatching(/not authenticated/i) });
  });

  it("returns 400 when message is empty", async () => {
    const res = await handleCampaignAgentStreamPost(new Request("http://localhost/x", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: "  " })
    }), {
      getSession: async () => ({ userId: "u1", tenantId: "acme", apiKey: "k" }),
      apiBaseUrl: "https://example.test",
      connect: vi.fn() as unknown as typeof fetch
    });
    expect(res.status).toBe(400);
    await expect(res.json()).resolves.toMatchObject({ error: "message is required" });
  });

  it("returns 400 on invalid JSON without calling connect", async () => {
    const connect = vi.fn();
    const res = await handleCampaignAgentStreamPost(
      new Request("http://localhost/x", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: "{not-json"
      }),
      {
        getSession: async () => session(),
        apiBaseUrl: "https://example.test",
        connect: connect as unknown as typeof fetch
      }
    );
    expect(res.status).toBe(400);
    expect(connect).not.toHaveBeenCalled();
  });

  it("returns 401 and does not call connect when session has no credentials", async () => {
    const connect = vi.fn();
    const res = await handleCampaignAgentStreamPost(jsonRequest({ message: "hello" }), {
      getSession: async () => session({ apiKey: undefined, accessToken: undefined }),
      apiBaseUrl: "https://example.test",
      connect: connect as unknown as typeof fetch
    });
    expect(res.status).toBe(401);
    expect(connect).not.toHaveBeenCalled();
  });

  it("returns 400 when tenant id cannot be resolved", async () => {
    const connect = vi.fn();
    const res = await handleCampaignAgentStreamPost(jsonRequest({ message: "hello" }), {
      getSession: async () => session({ tenantId: "  " }),
      apiBaseUrl: "https://example.test",
      connect: connect as unknown as typeof fetch
    });
    expect(res.status).toBe(400);
    expect(connect).not.toHaveBeenCalled();
  });

  it("returns 200 event-stream, forwards signal, linkedCampaignId, and clientMessageId", async () => {
    vi.spyOn(console, "info").mockImplementation(() => {});
    const abort = new AbortController();
    const connect = vi.fn(
      async () =>
        new Response('event: done\ndata: {"conversationId":"c1"}\n\n', {
          status: 200,
          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
        })
    );
    const request = jsonRequest(
      {
        message: "  hello  ",
        conversationId: "conv-1",
        linkedCampaignId: "camp-1",
        clientMessageId: "message-1"
      },
      { signal: abort.signal }
    );
    const res = await handleCampaignAgentStreamPost(request, {
      getSession: async () => session(),
      apiBaseUrl: "https://example.test",
      connect: connect as unknown as typeof fetch
    });
    expect(res.status).toBe(200);
    expect(res.headers.get("Content-Type")).toBe("text/event-stream; charset=utf-8");
    expect(res.headers.get("Cache-Control")).toBe("no-cache, no-transform");
    expect(res.headers.get("Connection")).toBe("keep-alive");
    expect(res.headers.get("X-Accel-Buffering")).toBe("no");
    const body = await readResponseText(res);
    expect(body).toContain("event: done");
    expect(connect).toHaveBeenCalledTimes(1);
    const [url, init] = connect.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("https://example.test/api/v1/acme/campaign-agent/messages/stream");
    expect(init.method).toBe("POST");
    expect(init.signal).toBe(request.signal);
    const sent = JSON.parse(String(init.body)) as Record<string, unknown>;
    expect(sent).toEqual({
      message: "hello",
      conversationId: "conv-1",
      linkedCampaignId: "camp-1",
      clientMessageId: "message-1"
    });
    expect(res.headers.get("Content-Type")).not.toMatch(/application\/json/);
  });

  it("omits nullable stream identifiers", async () => {
    vi.spyOn(console, "info").mockImplementation(() => {});
    const connect = vi.fn(
      async () =>
        new Response("event: done\ndata: {}\n\n", {
          status: 200,
          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
        })
    );
    const res = await handleCampaignAgentStreamPost(
      jsonRequest({
        message: "hello",
        conversationId: null,
        linkedCampaignId: null,
        clientMessageId: null
      }),
      {
        getSession: async () => session(),
        apiBaseUrl: "https://example.test",
        connect: connect as unknown as typeof fetch
      }
    );
    await readResponseText(res);
    const [, init] = connect.mock.calls[0] as unknown as [string, RequestInit];
    expect(JSON.parse(String(init.body))).toEqual({ message: "hello" });
  });

  it("logs once when the response body is cancelled before the stream ends", async () => {
    const info = vi.spyOn(console, "info").mockImplementation(() => {});
    const connect = vi.fn(
      async () =>
        new Response(
          new ReadableStream({
            start(controller) {
              controller.enqueue(new TextEncoder().encode("event: chunk\ndata: {}\n\n"));
            }
          }),
          {
            status: 200,
            headers: { "Content-Type": "text/event-stream; charset=utf-8" }
          }
        )
    );
    const res = await handleCampaignAgentStreamPost(
      jsonRequest({ message: "do not log this prompt", conversationId: "conv-cancel" }),
      {
        getSession: async () => session({ userId: "user-cancel" }),
        apiBaseUrl: "https://example.test",
        connect: connect as unknown as typeof fetch
      }
    );
    const reader = res.body!.getReader();
    await reader.read();
    await reader.cancel();
    expect(info).toHaveBeenCalledTimes(1);
    expect(info).toHaveBeenCalledWith({
      tenantId: "acme",
      conversationId: "conv-cancel",
      userId: "user-cancel",
      status: 200
    });
    const serialized = JSON.stringify(info.mock.calls);
    expect(serialized).not.toMatch(/do not log this prompt/);
  });

  it("logs tenantId, conversationId, userId, and status when the stream ends", async () => {
    const info = vi.spyOn(console, "info").mockImplementation(() => {});
    const connect = vi.fn(
      async () =>
        new Response("event: done\ndata: {}\n\n", {
          status: 200,
          headers: { "Content-Type": "text/event-stream; charset=utf-8" }
        })
    );
    const res = await handleCampaignAgentStreamPost(
      jsonRequest({ message: "do not log this prompt", conversationId: "conv-9" }),
      {
        getSession: async () => session({ userId: "user-42", apiKey: "super-secret-key" }),
        apiBaseUrl: "https://example.test",
        connect: connect as unknown as typeof fetch
      }
    );
    await readResponseText(res);
    expect(info).toHaveBeenCalledTimes(1);
    expect(info).toHaveBeenCalledWith({
      tenantId: "acme",
      conversationId: "conv-9",
      userId: "user-42",
      status: 200
    });
    const serialized = JSON.stringify(info.mock.calls);
    expect(serialized).not.toMatch(/do not log this prompt/);
    expect(serialized).not.toMatch(/super-secret-key/);
    expect(serialized).not.toMatch(/Authorization/i);
  });
});
