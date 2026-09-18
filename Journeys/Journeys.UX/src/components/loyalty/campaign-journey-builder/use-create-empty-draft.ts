"use client";

import { useCallback, useState } from "react";
import toast from "react-hot-toast";

import type { Campaign } from "@/lib/campaign-types";
import { updateCampaign } from "@/services/loyalty/actions";

import { useJourneyBuilderActions } from "./journey-builder-store";

export function buildEmptyDraftPayload(): Campaign {
  return {
    name: "Untitled campaign",
    status: "draft",
    startDate: new Date().toISOString()
  };
}

export function useCreateEmptyDraft(): {
  createEmptyDraft: () => Promise<Campaign | null>;
  creating: boolean;
} {
  const actions = useJourneyBuilderActions();
  const [creating, setCreating] = useState(false);

  const createEmptyDraft = useCallback(async () => {
    setCreating(true);
    try {
      const payload = buildEmptyDraftPayload();
      const response = await updateCampaign(payload);
      if (!response.success || !response.data) {
        throw new Error(response.error ?? "Failed to create draft");
      }

      actions.hydrateFromCampaign({
        campaign: response.data as Campaign,
        builderPhase: "setup"
      });
      actions.setDraftPersistence("persisted");
      actions.setLinkedCampaignId(response.data.id ?? null);
      actions.setDirty(false);
      toast.success("Draft created");
      return response.data as Campaign;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Failed to create draft");
      return null;
    } finally {
      setCreating(false);
    }
  }, [actions]);

  return { createEmptyDraft, creating };
}
