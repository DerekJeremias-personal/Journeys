import Link from "next/link";

import { ArchivedCampaignsClient } from "@/components/loyalty/campaigns/archived-campaigns-client";

export default function ArchivedCampaignsPage() {
  return (
    <div className="space-y-6">
      <div>
        <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/campaigns">
          ← Campaigns
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">Archived campaigns</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Restore creates a new draft with the same program identity. Archive rows stay unchanged.
        </p>
      </div>
      <ArchivedCampaignsClient />
    </div>
  );
}
