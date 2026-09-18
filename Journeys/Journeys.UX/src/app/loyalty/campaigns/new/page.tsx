import Link from "next/link";

import { CampaignJourneyBuilderShell } from "@/components/loyalty/campaign-journey-builder";

export default function NewCampaignPage() {
  return (
    <main className="space-y-5 p-6">
      <div>
        <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/campaigns">
          ← Campaigns
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">New campaign</h1>
        <p className="text-sm text-muted-foreground">
          Build the journey directly or work with the Agent in the side rail.
        </p>
      </div>
      <CampaignJourneyBuilderShell />
    </main>
  );
}
