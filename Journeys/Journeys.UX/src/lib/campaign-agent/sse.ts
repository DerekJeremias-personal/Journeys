export type SseEvent = { event: string; data: string };

/**
 * Append incoming bytes to a buffer and return complete SSE event blocks.
 */
export function pushSseBytes(buffer: string, chunk: string): { buffer: string; events: SseEvent[] } {
  buffer += chunk;
  const events: SseEvent[] = [];
  const blocks = buffer.split(/\r?\n\r?\n/);
  buffer = blocks.pop() ?? "";

  for (const block of blocks) {
    if (!block.trim()) continue;
    let eventName = "message";
    const dataLines: string[] = [];
    for (const line of block.split(/\r?\n/)) {
      if (line.startsWith("event:")) eventName = line.slice(6).trim();
      else if (line.startsWith("data:")) dataLines.push(line.slice(5).trimStart());
    }
    const data = dataLines.join("\n");
    if (data.length > 0) events.push({ event: eventName, data });
  }

  return { buffer, events };
}
