import type { JourneysSession } from "../journeys-fetch";
import { allowInsecureLocalHttps } from "../local-https";
import { resolveTenantId } from "../resolve-tenant-id";
import { authorizeCampaignAgentProxy, campaignAgentUpstreamHeaders } from "./proxy-auth";
import { createEarlySseProxyStream } from "./sse-early-proxy";
import { campaignAgentStreamUrl } from "./stream-url";

export type CampaignAgentStreamRouteDeps = {
  getSession: () => Promise<JourneysSession | null>;
  apiBaseUrl: string;
  connect: typeof fetch;
};

const SSE_HEADERS = {
  "Content-Type": "text/event-stream; charset=utf-8",
  "Cache-Control": "no-cache, no-transform",
  Connection: "keep-alive",
  "X-Accel-Buffering": "no"
} as const;

type StreamBody = {
  message?: string;
  conversationId?: string | null;
  linkedCampaignId?: string | null;
  clientMessageId?: string | null;
};

function jsonError(status: number, error: string): Response {
  return Response.json({ error }, { status });
}

function logWhenStreamEnds(
  stream: ReadableStream<Uint8Array>,
  payload: {
    tenantId: string;
    conversationId: string | undefined;
    userId: string;
    status: () => number;
  }
): ReadableStream<Uint8Array> {
  let logged = false;
  const logOnce = (): void => {
    if (logged) return;
    logged = true;
    console.info({
      tenantId: payload.tenantId,
      conversationId: payload.conversationId,
      userId: payload.userId,
      status: payload.status()
    });
  };

  const reader = stream
    .pipeThrough(
      new TransformStream<Uint8Array, Uint8Array>({
        flush() {
          logOnce();
        }
      })
    )
    .getReader();

  return new ReadableStream<Uint8Array>({
    async pull(controller) {
      const { done, value } = await reader.read();
      if (done) {
        controller.close();
      } else {
        controller.enqueue(value);
      }
    },
    cancel(reason) {
      logOnce();
      return reader.cancel(reason);
    }
  });
}

export async function handleCampaignAgentStreamPost(
  request: Request,
  deps: CampaignAgentStreamRouteDeps
): Promise<Response> {
  let body: StreamBody;
  try {
    body = (await request.json()) as StreamBody;
  } catch {
    return jsonError(400, "Invalid JSON body");
  }

  const trimmed = body.message?.trim() ?? "";
  if (!trimmed) {
    return jsonError(400, "message is required");
  }

  const auth = authorizeCampaignAgentProxy(await deps.getSession());
  if (!auth.ok) {
    return jsonError(auth.status, auth.error);
  }

  const tenantId = resolveTenantId(auth.session.tenantId);
  if (!tenantId) {
    return jsonError(400, "TenantId is required");
  }

  const upstreamUrl = campaignAgentStreamUrl(deps.apiBaseUrl, tenantId);
  allowInsecureLocalHttps(deps.apiBaseUrl);

  const conversationId = body.conversationId || undefined;
  const linkedCampaignId = body.linkedCampaignId || undefined;
  const clientMessageId = body.clientMessageId || undefined;
  let status = 0;
  const connect: typeof fetch = async (input, init) => {
    const response = await deps.connect(input, init);
    status = response.status;
    return response;
  };

  const stream = createEarlySseProxyStream({
    upstreamUrl,
    upstreamInit: {
      method: "POST",
      headers: campaignAgentUpstreamHeaders(auth.session),
      cache: "no-store",
      body: JSON.stringify({
        message: trimmed,
        ...(conversationId ? { conversationId } : {}),
        ...(linkedCampaignId ? { linkedCampaignId } : {}),
        ...(clientMessageId ? { clientMessageId } : {})
      })
    },
    signal: request.signal,
    connect
  });

  return new Response(
    logWhenStreamEnds(stream, {
      tenantId,
      conversationId,
      userId: auth.session.userId,
      status: () => status
    }),
    { status: 200, headers: SSE_HEADERS }
  );
}
