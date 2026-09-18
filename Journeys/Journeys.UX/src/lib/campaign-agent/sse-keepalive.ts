const DEFAULT_IDLE_MS = 15_000;
const DEFAULT_COMMENT = ": keep-alive";

export type SseKeepAliveOptions = {
  idleMs?: number;
  comment?: string;
};

/**
 * Wraps an upstream SSE byte stream and emits an SSE comment heartbeat when
 * no upstream bytes arrive within idleMs. Keeps App Gateway / proxies from
 * closing idle connections during long MCP tool phases.
 */
export function wrapStreamWithSseKeepAlive(
  upstream: ReadableStream<Uint8Array>,
  options: SseKeepAliveOptions = {}
): ReadableStream<Uint8Array> {
  const idleMs = options.idleMs ?? DEFAULT_IDLE_MS;
  const encoder = new TextEncoder();
  const keepAliveBytes = encoder.encode(`${options.comment ?? DEFAULT_COMMENT}\n\n`);
  const reader = upstream.getReader();

  let idleTimer: ReturnType<typeof setTimeout> | undefined;

  const clearIdleTimer = (): void => {
    if (idleTimer !== undefined) {
      clearTimeout(idleTimer);
      idleTimer = undefined;
    }
  };

  return new ReadableStream<Uint8Array>({
    start(controller) {
      const scheduleKeepAlive = (): void => {
        clearIdleTimer();
        idleTimer = setTimeout(() => {
          try {
            controller.enqueue(keepAliveBytes);
            scheduleKeepAlive();
          } catch {
            clearIdleTimer();
          }
        }, idleMs);
      };

      const pump = async (): Promise<void> => {
        scheduleKeepAlive();
        try {
          while (true) {
            const { done, value } = await reader.read();
            clearIdleTimer();
            if (done) {
              controller.close();
              return;
            }
            controller.enqueue(value);
            scheduleKeepAlive();
          }
        } catch (error) {
          controller.error(error);
        } finally {
          clearIdleTimer();
        }
      };

      void pump();
    },
    cancel(reason) {
      clearIdleTimer();
      return reader.cancel(reason);
    },
  });
}
