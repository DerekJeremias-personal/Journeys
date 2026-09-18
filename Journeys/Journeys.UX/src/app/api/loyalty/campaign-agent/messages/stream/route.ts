import { auth } from "@/auth";
import { handleCampaignAgentStreamPost } from "@/lib/campaign-agent/stream-route";
import type { JourneysSession } from "@/lib/journeys-fetch";

export const dynamic = "force-dynamic";
export const maxDuration = 600;

async function getSession(): Promise<JourneysSession | null> {
  const s = await auth();
  if (!s?.user?.id) return null;
  return {
    userId: s.user.id,
    tenantId: (s as { tenantId?: string }).tenantId ?? "",
    accessToken: (s as { accessToken?: string }).accessToken,
    apiKey: (s as { apiKey?: string }).apiKey
  };
}

export async function POST(request: Request) {
  return handleCampaignAgentStreamPost(request, {
    getSession,
    apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? "",
    connect: fetch
  });
}
