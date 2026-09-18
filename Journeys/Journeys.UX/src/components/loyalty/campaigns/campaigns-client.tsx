"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { toast } from "react-hot-toast";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { duplicateCampaign } from "@/lib/campaign-duplicate-action";
import {
  campaignKebabVisibility,
  normalizeCampaignStatus
} from "@/lib/campaign-kebab";
import { lifecycleSavePayload } from "@/lib/campaign-lifecycle-payload";
import type { Campaign } from "@/lib/campaign-types";
import {
  copyCampaign,
  deleteCampaign,
  getCampaigns,
  getCampaignsByFilters,
  restoreCampaign,
  updateCampaign
} from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import { CampaignCard, type CampaignAction } from "./campaign-card";
import {
  CampaignFilters,
  EMPTY_CAMPAIGN_FILTERS,
  type CampaignFiltersValue
} from "./campaign-filters";

function campaignHref(campaign: Campaign): string | undefined {
  if (!campaign.id) return undefined;
  const status = normalizeCampaignStatus(campaign.status);
  return `/loyalty/campaigns/${campaign.id}?campaignStatus=${encodeURIComponent(status)}`;
}

export function CampaignsClient() {
  const queryClient = useQueryClient();
  const router = useRouter();
  const [filters, setFilters] = useState<CampaignFiltersValue>(EMPTY_CAMPAIGN_FILTERS);
  const [deleteTarget, setDeleteTarget] = useState<Campaign | null>(null);
  const [publishTarget, setPublishTarget] = useState<Campaign | null>(null);
  const useFiltered = filters.queryString.length > 0;

  const listQuery = useQuery({
    queryKey: useFiltered
      ? loyaltyKeys.campaigns.list({
          queryString: filters.queryString,
          queryParams: filters.queryParams
        })
      : loyaltyKeys.campaigns.all,
    queryFn: async () => {
      const result = useFiltered
        ? await getCampaignsByFilters({
            query: filters.queryString,
            parameters: filters.queryParams,
            pageSize: 100,
            sortOrder: "DESC"
          })
        : await getCampaigns();
      if (!result.success) throw new Error(result.error ?? "Failed to load campaigns");
      return result.data ?? [];
    }
  });

  const invalidateCampaigns = () =>
    queryClient.invalidateQueries({ queryKey: loyaltyKeys.campaigns.all });

  const lifecycleMutation = useMutation({
    mutationFn: async ({
      campaign,
      action
    }: {
      campaign: Campaign;
      action: "publish" | "unpublish" | "archive";
    }) => {
      const result = await updateCampaign(lifecycleSavePayload(campaign, action));
      if (!result.success) throw new Error(result.error ?? "Failed to update campaign");
      if (!result.data) throw new Error("Update returned an empty campaign");
      return result.data;
    },
    onSuccess: (campaign) => {
      void invalidateCampaigns();
      setPublishTarget(null);
      const status = normalizeCampaignStatus(campaign.status);
      toast.success(
        status === "archive"
          ? "Campaign archived"
          : status === "pause"
            ? "Campaign paused"
            : "Campaign published"
      );
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Failed to update campaign");
    }
  });

  const duplicateMutation = useMutation({
    mutationFn: async (campaign: Campaign) => {
      if (!campaign.id) throw new Error("Campaign id missing");
      const status = normalizeCampaignStatus(campaign.status) || "draft";
      const result = await duplicateCampaign({
        id: campaign.id,
        status,
        copyCampaign,
        updateCampaign
      });
      if (!result.success) throw new Error(result.error ?? "Failed to duplicate campaign");
      return result.data;
    },
    onSuccess: () => {
      void invalidateCampaigns();
      toast.success("Campaign duplicated");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Failed to duplicate campaign");
    }
  });

  const restoreMutation = useMutation({
    mutationFn: async (campaign: Campaign) => {
      if (!campaign.id) throw new Error("Campaign id missing");
      const result = await restoreCampaign(campaign.id);
      if (!result.success) throw new Error(result.error ?? "Failed to restore campaign");
      return result.data;
    },
    onSuccess: () => {
      void invalidateCampaigns();
      toast.success("Campaign restored to draft");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Failed to restore campaign");
    }
  });

  const deleteMutation = useMutation({
    mutationFn: async (campaign: Campaign) => {
      if (!campaign.id) throw new Error("Campaign id missing");
      const status = normalizeCampaignStatus(campaign.status) || "draft";
      if (!campaignKebabVisibility(status, campaign.extCampaignId ?? undefined).delete) {
        throw new Error("Only draft campaigns can be deleted");
      }
      const result = await deleteCampaign(campaign.id, status);
      if (!result.success) throw new Error(result.error ?? "Failed to delete campaign");
      return campaign.id;
    },
    onSuccess: () => {
      void invalidateCampaigns();
      setDeleteTarget(null);
      toast.success("Campaign deleted");
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : "Failed to delete campaign");
    }
  });

  const handleAction = (action: CampaignAction, campaign: Campaign) => {
    switch (action) {
      case "edit": {
        const href = campaignHref(campaign);
        if (!href) {
          toast.error("Campaign id missing");
          return;
        }
        router.push(href);
        return;
      }
      case "duplicate":
        if (duplicateMutation.isPending) return;
        duplicateMutation.mutate(campaign);
        return;
      case "publish":
        setPublishTarget(campaign);
        return;
      case "unpublish":
      case "archive":
        lifecycleMutation.mutate({ campaign, action });
        return;
      case "restore":
        restoreMutation.mutate(campaign);
        return;
      case "versions":
        if (!campaign.extCampaignId) {
          toast.error("Campaign has no external id for version history");
          return;
        }
        router.push(
          `/loyalty/campaigns/versions/${encodeURIComponent(campaign.extCampaignId)}`
        );
        return;
      case "delete":
        setDeleteTarget(campaign);
        return;
    }
  };

  const campaigns = useMemo(() => listQuery.data ?? [], [listQuery.data]);
  const sortedCampaigns = useMemo(
    () =>
      [...campaigns].sort((left, right) => {
        const leftTime = new Date(left.startDate ?? 0).getTime();
        const rightTime = new Date(right.startDate ?? 0).getTime();
        return rightTime - leftTime;
      }),
    [campaigns]
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">Campaigns</h2>
        <div className="flex items-center gap-2">
          <Button asChild variant="outline">
            <Link href="/loyalty/campaigns/archived">Archived</Link>
          </Button>
          <Button asChild>
            <Link href="/loyalty/campaigns/new">
              <Plus className="mr-2 h-4 w-4" />
              New campaign
            </Link>
          </Button>
        </div>
      </div>

      <CampaignFilters value={filters} onChange={setFilters} />

      {listQuery.isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <Card key={index}>
              <CardContent className="h-40 p-6">
                <Skeleton className="h-full w-full" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : listQuery.isError ? (
        <Alert variant="destructive">
          <AlertTitle>Failed to load campaigns</AlertTitle>
          <AlertDescription>
            {listQuery.error instanceof Error ? listQuery.error.message : "Unknown error"}
          </AlertDescription>
        </Alert>
      ) : sortedCampaigns.length === 0 ? (
        <Card>
          <CardContent className="p-12 text-center text-sm text-muted-foreground">
            No campaigns match these filters.
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {sortedCampaigns.map((campaign, index) => (
            <CampaignCard
              key={campaign.id ?? campaign.extCampaignId ?? campaign.name ?? index}
              campaign={campaign}
              onAction={handleAction}
              detailHref={campaignHref(campaign)}
            />
          ))}
        </div>
      )}

      <AlertDialog
        open={publishTarget !== null}
        onOpenChange={(open) => {
          if (!open) setPublishTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Publish to Live?</AlertDialogTitle>
            <AlertDialogDescription>
              This campaign will become available for live event processing.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (publishTarget) {
                  lifecycleMutation.mutate({ campaign: publishTarget, action: "publish" });
                }
              }}
              disabled={lifecycleMutation.isPending}
            >
              {lifecycleMutation.isPending ? "Publishing…" : "Publish"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete campaign?</AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget
                ? `This will permanently delete "${deleteTarget.name || "Untitled campaign"}".`
                : null}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              onClick={() => {
                if (deleteTarget) deleteMutation.mutate(deleteTarget);
              }}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending ? "Deleting…" : "Delete"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
