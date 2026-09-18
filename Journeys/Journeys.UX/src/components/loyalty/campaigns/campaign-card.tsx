"use client";

import { Calendar, MoreVertical } from "lucide-react";
import Link from "next/link";

import { StatusBadge } from "@/components/loyalty/status-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { campaignKebabVisibility, normalizeCampaignStatus } from "@/lib/campaign-kebab";
import type { Campaign } from "@/lib/campaign-types";

export type CampaignAction =
  | "edit"
  | "duplicate"
  | "publish"
  | "unpublish"
  | "archive"
  | "restore"
  | "versions"
  | "delete";

export interface CampaignCardProps {
  campaign: Campaign;
  onAction: (action: CampaignAction, campaign: Campaign) => void;
  detailHref?: string;
}

function formatDate(value?: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
}

export function CampaignCard({ campaign, onAction, detailHref }: CampaignCardProps) {
  const status = normalizeCampaignStatus(campaign.status);
  const visibility = campaignKebabVisibility(status, campaign.extCampaignId ?? undefined);
  const name = campaign.name || "Untitled campaign";
  const title = detailHref ? (
    <Link href={detailHref} className="hover:underline">
      {name}
    </Link>
  ) : (
    name
  );

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
        <div className="min-w-0 space-y-2">
          <CardTitle className="truncate text-base">{title}</CardTitle>
          <div className="flex items-center gap-2">
            <StatusBadge status={status} />
            {campaign.extCampaignId ? (
              <span
                className="truncate font-mono text-xs text-muted-foreground"
                title={campaign.extCampaignId}
              >
                {campaign.extCampaignId}
              </span>
            ) : null}
          </div>
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" aria-label={`Actions for ${name}`}>
              <MoreVertical className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {visibility.edit ? (
              <DropdownMenuItem onClick={() => onAction("edit", campaign)}>Edit</DropdownMenuItem>
            ) : null}
            {visibility.agent && campaign.id ? (
              <DropdownMenuItem asChild>
                <Link
                  href={`/loyalty/campaigns/${campaign.id}/agent?campaignStatus=${encodeURIComponent(status)}`}
                >
                  Open in Agent
                </Link>
              </DropdownMenuItem>
            ) : null}
            {visibility.duplicate ? (
              <DropdownMenuItem onClick={() => onAction("duplicate", campaign)}>
                Duplicate
              </DropdownMenuItem>
            ) : null}
            {visibility.versions ? (
              <DropdownMenuItem onClick={() => onAction("versions", campaign)}>
                Versions
              </DropdownMenuItem>
            ) : null}
            {visibility.publish ? (
              <DropdownMenuItem onClick={() => onAction("publish", campaign)}>
                Publish
              </DropdownMenuItem>
            ) : null}
            {visibility.unpublish ? (
              <DropdownMenuItem onClick={() => onAction("unpublish", campaign)}>
                Unpublish
              </DropdownMenuItem>
            ) : null}
            {visibility.archive ? (
              <DropdownMenuItem onClick={() => onAction("archive", campaign)}>
                Archive
              </DropdownMenuItem>
            ) : null}
            {visibility.restore ? (
              <DropdownMenuItem onClick={() => onAction("restore", campaign)}>
                Restore to draft
              </DropdownMenuItem>
            ) : null}
            {visibility.delete ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={() => onAction("delete", campaign)}
                  variant="destructive"
                >
                  Delete
                </DropdownMenuItem>
              </>
            ) : null}
          </DropdownMenuContent>
        </DropdownMenu>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <div className="flex items-center gap-2 text-muted-foreground">
          <Calendar className="h-4 w-4" aria-hidden="true" />
          <span>
            {formatDate(campaign.startDate)} – {formatDate(campaign.endDate)}
          </span>
        </div>
      </CardContent>
    </Card>
  );
}
