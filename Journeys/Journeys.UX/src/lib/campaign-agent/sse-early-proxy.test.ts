import { describe, expect, it, vi } from "vitest";
import { createEarlySseProxyStream } from "./sse-early-proxy";
import { pushSseBytes } from "./sse";

async function readAll(stream: ReadableStream<Uint8Array>): Promise<string> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let out = "";
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    if (value) out += decoder.decode(value, { stream: true });
  }
  return out;
}

describe("createEarlySseProxyStream", () => {
  it("maps 401 to an SSE error and does not throw", async () => {
    const connect = vi.fn(async () => new Response(JSON.stringify({ error: "nope" }), { status: 401 }));
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    const { events } = pushSseBytes("", text);
    const err = events.find((e) => e.event === "error");
    expect(err).toBeTruthy();
    expect(err!.data.toLowerCase()).toMatch(/not authorized|nope|401/);
  });

  it("maps connect failure to Cannot reach Journeys.API", async () => {
    const connect = vi.fn(async () => {
      throw new Error("fetch failed");
    });
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    expect(text).toMatch(/Cannot reach Journeys\.API/);
    expect(text).not.toMatch(/Coach/i);
  });

  it("forwards upstream SSE bytes on 200 event-stream", async () => {
    const body = "event: started\ndata: {\"conversationId\":\"c1\"}\n\n";
    const connect = vi.fn(
      async () =>
        new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream; charset=utf-8" } })
    );
    const stream = createEarlySseProxyStream({
      upstreamUrl: "https://example.test/stream",
      upstreamInit: { method: "POST", body: "{}" },
      connect: connect as unknown as typeof fetch,
      idleMs: 60_000
    });
    const text = await readAll(stream);
    expect(text).toContain("event: started");
    expect(text).toContain("c1");
  });
});
