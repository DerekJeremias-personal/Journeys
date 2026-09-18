import Link from "next/link";

import { CampaignVersionsClient } from "@/components/loyalty/campaigns/campaign-versions-client";

export default async function CampaignVersionsPage({
  params
}: {
  params: Promise<{ extCampaignId: string }>;
}) {
  const { extCampaignId } = await params;
  const decodedExtCampaignId = decodeURIComponent(extCampaignId);

  return (
    <div className="space-y-6">
      <div>
        <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/campaigns">
          ← Campaigns
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">Campaign versions</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Version history for this program identity.
        </p>
      </div>
      <CampaignVersionsClient extCampaignId={decodedExtCampaignId} />
    </div>
  );
}
