import { allowInsecureLocalHttps, describeFetchFailure } from "../local-https";
import { wrapStreamWithSseKeepAlive } from "./sse-keepalive";

export type EarlySseProxyOptions = {
  upstreamUrl: string;
  upstreamInit: RequestInit;
  idleMs?: number;
  signal?: AbortSignal;
  connect?: typeof fetch;
};

const MAX_ERROR_BODY_CHARS = 500;

function encodeSseError(message: string): Uint8Array {
  const encoder = new TextEncoder();
  return encoder.encode(`event: error\ndata: ${JSON.stringify({ message })}\n\n`);
}

function httpErrorMessage(status: number, body: string): string {
  if (status === 401 || status === 403) return "not authorized / check tenant or key";
  const truncated = body.slice(0, MAX_ERROR_BODY_CHARS);
  return truncated || `Upstream HTTP ${status}`;
}

export function createEarlySseProxyStream(options: EarlySseProxyOptions): ReadableStream<Uint8Array> {
  const idleMs = options.idleMs ?? 15_000;
  const connect = options.connect ?? fetch;

  const inner = new ReadableStream<Uint8Array>({
    start(controller) {
      void (async () => {
        try {
          allowInsecureLocalHttps(options.upstreamUrl);
          let response: Response;
          try {
            response = await connect(options.upstreamUrl, {
              ...options.upstreamInit,
              signal: options.signal ?? options.upstreamInit.signal
            });
          } catch (error) {
            controller.enqueue(encodeSseError(`Cannot reach Journeys.API (${describeFetchFailure(error)})`));
            controller.close();
            return;
          }

          if (response.status < 200 || response.status >= 300) {
            const body = await response.text();
            controller.enqueue(encodeSseError(httpErrorMessage(response.status, body)));
            controller.close();
            return;
          }

          const upstreamBody = response.body;
          if (!upstreamBody) {
            controller.close();
            return;
          }

          const reader = upstreamBody.getReader();
          while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            if (value) controller.enqueue(value);
          }
          controller.close();
        } catch (error) {
          if ((error as Error).name === "AbortError") {
            try {
              controller.close();
            } catch {
              /* already closed */
            }
            return;
          }
          try {
            controller.enqueue(encodeSseError(`Cannot reach Journeys.API (${describeFetchFailure(error)})`));
            controller.close();
          } catch {
            /* already closed */
          }
        }
      })();
    }
  });

  return wrapStreamWithSseKeepAlive(inner, { idleMs });
}
