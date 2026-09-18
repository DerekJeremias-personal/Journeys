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
