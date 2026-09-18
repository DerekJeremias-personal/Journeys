import Link from "next/link";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { CampaignWizard } from "@/components/loyalty/campaigns/wizard/campaign-wizard";
import { resolveWizardCampaign } from "@/lib/campaign-live-edit";
import { normalizeCampaignStatus } from "@/lib/campaign-kebab";
import type { Campaign } from "@/lib/campaign-types";
import { getCampaign, getDraftByExt } from "@/services/loyalty/actions";

function CampaignNotFound({ message }: { message: string }) {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Edit campaign</h1>
        <p className="mt-1 text-sm text-muted-foreground">Wizard edit requires a campaign partition.</p>
      </div>
      <Card>
        <CardContent className="p-8">
          <Alert variant="destructive" className="mb-4">
            <AlertTitle>Campaign not found</AlertTitle>
            <AlertDescription>{message}</AlertDescription>
          </Alert>
          <Button asChild variant="outline">
            <Link href="/loyalty/campaigns">← Back to campaigns</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

export default async function EditCampaignPage({
  params,
  searchParams
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ campaignStatus?: string | string[] }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const rawStatus = typeof query.campaignStatus === "string" ? query.campaignStatus : "";
  const campaignStatus = normalizeCampaignStatus(rawStatus);

  if (!campaignStatus) {
    return (
      <CampaignNotFound message="campaignStatus is required to open the campaign wizard." />
    );
  }

  const resolved = await resolveWizardCampaign({
    requested: { id, status: campaignStatus },
    getCampaign,
    getDraftByExt
  });

  if (resolved.mode === "edit") {
    const result = await getCampaign(resolved.id, resolved.status);
    if (!result.success || !result.data) {
      return (
        <CampaignNotFound message={result.error ?? "No campaign exists with that ID."} />
      );
    }
    return (
      <div className="space-y-6">
        <div>
          <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/campaigns">
            ← Campaigns
          </Link>
          <h1 className="mt-2 text-2xl font-semibold tracking-tight">
            Edit: {result.data.name ?? id}
          </h1>
          <p className="text-sm text-muted-foreground">Wizard edit for this campaign partition.</p>
        </div>
        <CampaignWizard mode="edit" saveMode="edit" initial={result.data as Campaign} />
      </div>
    );
  }

  const live = await getCampaign(id, "live");
  if (!live.success || !live.data) {
    return <CampaignNotFound message={live.error ?? "No campaign exists with that ID."} />;
  }

  const initial: Campaign = {
    ...(live.data as Campaign),
    id: undefined,
    etag: undefined,
    status: "draft",
    extCampaignId: resolved.extCampaignId
  };

  return (
    <div className="space-y-6">
      <div>
        <Link className="text-sm text-muted-foreground hover:text-foreground" href="/loyalty/campaigns">
          ← Campaigns
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">
          Edit: {live.data.name ?? id}
        </h1>
        <p className="text-sm text-muted-foreground">
          Saving creates a new draft with the same program identity.
        </p>
      </div>
      <CampaignWizard mode="edit" saveMode="new-draft-same-ext" initial={initial} />
    </div>
  );
}
