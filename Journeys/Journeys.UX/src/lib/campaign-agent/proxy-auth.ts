import type { JourneysSession } from "../journeys-fetch";

export type CampaignAgentProxyAuth =
  | { ok: true; session: JourneysSession }
  | { ok: false; status: 401; error: string };

export function authorizeCampaignAgentProxy(session: JourneysSession | null): CampaignAgentProxyAuth {
  if (!session?.userId) return { ok: false, status: 401, error: "Not authenticated" };
  if (!session.accessToken && !session.apiKey) {
    return { ok: false, status: 401, error: "No credentials in session" };
  }
  return { ok: true, session };
}

export function campaignAgentUpstreamHeaders(session: JourneysSession): Record<string, string> {
  const headers: Record<string, string> = {
    Accept: "text/event-stream",
    "Content-Type": "application/json"
  };
  if (session.accessToken) headers.Authorization = `Bearer ${session.accessToken}`;
  else if (session.apiKey) headers["Journeys-API-KEY"] = session.apiKey;
  return headers;
}
