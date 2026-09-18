"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo } from "react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import type { Campaign } from "@/lib/campaign-types";
import { getCampaignVersions } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

export interface CampaignVersionsClientProps {
  extCampaignId: string;
}

export function CampaignVersionsClient({ extCampaignId }: CampaignVersionsClientProps) {
  const versionsQuery = useQuery({
    queryKey: loyaltyKeys.campaigns.versions(extCampaignId),
    queryFn: async () => {
      const result = await getCampaignVersions(extCampaignId);
      if (!result.success) throw new Error(result.error ?? "Failed to load campaign versions");
      return (result.data ?? []) as Campaign[];
    }
  });

  const versions = useMemo(() => {
    return [...(versionsQuery.data ?? [])].sort((left, right) => {
      const leftTime = new Date(left.startDate ?? 0).getTime();
      const rightTime = new Date(right.startDate ?? 0).getTime();
      return rightTime - leftTime;
    });
  }, [versionsQuery.data]);

  if (versionsQuery.isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 5 }).map((_, index) => (
          <Skeleton key={index} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  if (versionsQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{(versionsQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Version history for external campaign id: {extCampaignId}
        </p>
        <Button asChild variant="outline">
          <Link href="/loyalty/campaigns">Back to campaigns</Link>
        </Button>
      </div>

      {versions.length === 0 ? (
        <Card>
          <CardContent className="p-8 text-center text-sm text-muted-foreground">
            No versions found.
          </CardContent>
        </Card>
      ) : (
        <ul className="space-y-2">
          {versions.map((campaign, index) => (
            <li key={campaign.id ?? `${campaign.extCampaignId}-${index}`} className="rounded-md border p-3">
              <div className="flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{campaign.name}</p>
                  <p className="text-xs text-muted-foreground">
                    Status: {campaign.status} · Start:{" "}
                    {campaign.startDate ? new Date(campaign.startDate).toLocaleDateString() : "—"}
                  </p>
                </div>
                {campaign.id ? (
                  <Button asChild size="sm" variant="ghost">
                    <Link
                      href={`/loyalty/campaigns/${campaign.id}?campaignStatus=${encodeURIComponent(campaign.status ?? "")}`}
                    >
                      Open
                    </Link>
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
