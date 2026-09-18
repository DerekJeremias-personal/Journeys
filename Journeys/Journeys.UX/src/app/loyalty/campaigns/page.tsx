import Link from "next/link";
import { CampaignsClient } from "@/components/loyalty/campaigns";
import { listAgentConversations } from "@/services/loyalty/actions";

export default async function CampaignsPage() {
  const conversations = await listAgentConversations();
  const conversationId = conversations.success
    ? conversations.data?.[0]?.conversationId
    : undefined;

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Campaigns</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Build and manage customer journey campaigns.
          </p>
        </div>
        <div className="flex items-center gap-4">
          {conversationId ? (
            <Link
              href={`/loyalty/campaigns/agent?conversationId=${encodeURIComponent(conversationId)}`}
              className="text-sm font-medium text-primary underline-offset-4 hover:underline"
            >
              Resume agent
            </Link>
          ) : null}
          <Link
            href="/loyalty/campaigns/agent"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            Agent
          </Link>
        </div>
      </div>

      <CampaignsClient />
    </div>
  );
}
