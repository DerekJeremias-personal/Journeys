"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo } from "react";
import { toast } from "react-hot-toast";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { restoreArchived } from "@/lib/campaign-restore-action";
import type { Campaign } from "@/lib/campaign-types";
import { getArchivedCampaigns, restoreCampaign, updateCampaign } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import { CampaignCard, type CampaignAction } from "./campaign-card";

function archivedSortTime(campaign: Campaign): number {
  const archivedDate = typeof campaign.archivedDate === "string" ? campaign.archivedDate : undefined;
  return new Date(archivedDate ?? campaign.endDate ?? campaign.startDate ?? 0).getTime();
}

export function ArchivedCampaignsClient() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const archivedQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.archived(),
    queryFn: async () => {
      const result = await getArchivedCampaigns();
      if (!result.success) throw new Error(result.error ?? "Failed to load archived campaigns");
      return (result.data ?? []) as Campaign[];
    }
  });

  const restoreMutation = useMutation({
    mutationFn: async (campaign: Campaign) => {
      if (!campaign.id) throw new Error("Campaign id missing");
      const result = await restoreArchived({
        id: campaign.id,
        restoreCampaign,
        updateCampaign: (campaignToSave) =>
          updateCampaign(campaignToSave as Campaign)
      });
      if (!result.success) throw new Error(result.error ?? "Failed to restore campaign");
      return result;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.all });
      void queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.archived() });
      toast.success("Campaign restored to draft");
    },
    onError: (error: Error) => {
      toast.error(error.message);
    }
  });

  const campaigns = useMemo(() => {
    return [...(archivedQuery.data ?? [])].sort(
      (left, right) => archivedSortTime(right) - archivedSortTime(left)
    );
  }, [archivedQuery.data]);

  const handleAction = (action: CampaignAction, campaign: Campaign) => {
    if (action === "restore") {
      restoreMutation.mutate(campaign);
      return;
    }
    if (action === "versions") {
      if (!campaign.extCampaignId) {
        toast.error("Campaign has no external id for version history.");
        return;
      }
      router.push(`/loyalty/campaigns/versions/${encodeURIComponent(campaign.extCampaignId)}`);
    }
  };

  if (archivedQuery.isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, index) => (
          <Card key={index}>
            <CardContent className="h-40 p-6">
              <Skeleton className="h-full w-full" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (archivedQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{(archivedQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Archived campaigns can be restored to draft or inspected via version history.
        </p>
        <Button asChild variant="outline">
          <Link href="/loyalty/campaigns">Back to campaigns</Link>
        </Button>
      </div>
      {campaigns.length === 0 ? (
        <Card>
          <CardContent className="p-8 text-center text-sm text-muted-foreground">
            No archived campaigns found.
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {campaigns.map((campaign, index) => (
            <CampaignCard
              key={campaign.id ?? campaign.extCampaignId ?? campaign.name ?? index}
              campaign={campaign}
              onAction={handleAction}
            />
          ))}
        </div>
      )}
    </div>
  );
}
