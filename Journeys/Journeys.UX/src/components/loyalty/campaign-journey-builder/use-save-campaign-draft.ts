"use client";

import { useCallback, useState } from "react";
import toast from "react-hot-toast";

import type { CampaignListItem } from "@/lib/api-types";
import type { Campaign } from "@/lib/campaign-types";
import { updateCampaign, validateCampaign } from "@/services/loyalty/actions";

import {
  selectBuilderPhase,
  selectCampaign,
  useJourneyBuilderActions,
  useJourneyBuilderStore
} from "./journey-builder-store";

export function useSaveCampaignDraft(): {
  saveDraft: () => Promise<void>;
  saving: boolean;
  canSave: boolean;
} {
  const campaign = useJourneyBuilderStore(selectCampaign);
  const actions = useJourneyBuilderActions();
  const [saving, setSaving] = useState(false);

  const saveDraft = useCallback(async () => {
    const current = useJourneyBuilderStore.getState().campaign;
    if (!current) {
      toast.error("Nothing to save yet.");
      return;
    }

    setSaving(true);
    try {
      const payload: Campaign = {
        ...current,
        name: current.name?.trim() || "Untitled campaign",
        status: current.status ?? "draft"
      };
      const validation = await validateCampaign(payload as CampaignListItem);
      if (!validation.success) {
        throw new Error(validation.error ?? "Campaign validation failed.");
      }

      const response = await updateCampaign(payload);
      if (!response.success || !response.data) {
        throw new Error(response.error ?? "Failed to save draft");
      }

      actions.hydrateFromCampaign({
        campaign: response.data as Campaign,
        builderPhase: selectBuilderPhase(useJourneyBuilderStore.getState())
      });
      actions.setDraftPersistence("persisted");
      if (response.data.id) {
        actions.setLinkedCampaignId(response.data.id);
      }
      actions.setDirty(false);
      toast.success("Draft saved");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Failed to save draft");
    } finally {
      setSaving(false);
    }
  }, [actions]);

  return {
    saveDraft,
    saving,
    canSave: Boolean(campaign)
  };
}
