"use client";

import { useCallback, useEffect, useState } from "react";

import type { Campaign } from "@/lib/campaign-types";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { getCampaigns } from "@/services/loyalty/actions";

export interface LinkDraftDialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onSelect: (campaignId: string) => void;
}

export function LinkDraftDialog({ open, onOpenChange, onSelect }: LinkDraftDialogProps) {
  const [drafts, setDrafts] = useState<Campaign[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadDrafts = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await getCampaigns();
      if (!result.success || !result.data) {
        throw new Error(result.error ?? "Failed to load campaigns");
      }
      const draftOnly = result.data.filter(
        (campaign) => campaign.status?.toLowerCase() === "draft" && campaign.id?.trim()
      );
      setDrafts(draftOnly as Campaign[]);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Failed to load campaigns");
      setDrafts([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      void loadDrafts();
    }
  }, [open, loadDrafts]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Link draft campaign</DialogTitle>
          <DialogDescription>
            Choose the draft the Agent created. The journey builder canvas will load from that campaign.
          </DialogDescription>
        </DialogHeader>

        {loading ? <p className="text-sm text-muted-foreground">Loading drafts…</p> : null}
        {error ? <p className="text-sm text-destructive">{error}</p> : null}

        {!loading && !error && drafts.length === 0 ? (
          <p className="text-sm text-muted-foreground">No draft campaigns found.</p>
        ) : null}

        <ul className="max-h-64 space-y-2 overflow-auto">
          {drafts.map((draft) => {
            const id = draft.id;
            if (!id) {
              return null;
            }
            return (
              <li key={id}>
                <Button
                  type="button"
                  variant="outline"
                  className="h-auto w-full justify-start px-3 py-2 text-left"
                  onClick={() => {
                    onSelect(id);
                    onOpenChange(false);
                  }}
                >
                  <span className="font-medium">{draft.name?.trim() || "Untitled campaign"}</span>
                  {draft.extCampaignId ? (
                    <span className="ml-2 text-xs text-muted-foreground">{draft.extCampaignId}</span>
                  ) : null}
                </Button>
              </li>
            );
          })}
        </ul>
      </DialogContent>
    </Dialog>
  );
}
