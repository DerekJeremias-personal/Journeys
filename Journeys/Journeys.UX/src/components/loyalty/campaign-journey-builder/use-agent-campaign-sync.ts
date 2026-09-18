"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import toast from "react-hot-toast";

import { hydrateDecision } from "@/lib/campaign-agent/hydrate-policy";
import type { Campaign } from "@/lib/campaign-types";
import { getCampaign } from "@/services/loyalty/actions";

import { inferBuilderPhase } from "./infer-builder-phase";
import { useJourneyBuilderStore } from "./journey-builder-store";

export function decideAgentHydrate(args: {
  isDirty: boolean;
  campaignId: string;
  campaign: Campaign;
  hydrate: (campaign: Campaign) => void;
  conflict: (campaignId: string, campaign: Campaign) => void;
}): "apply" | "conflict" {
  const decision = hydrateDecision(args.isDirty);
  if (decision === "conflict") {
    args.conflict(args.campaignId, args.campaign);
  } else {
    args.hydrate(args.campaign);
  }
  return decision;
}

export function shouldAutoFetchLinkedCampaign(
  linkedCampaignId: string | null,
  storeCampaignId: string | null | undefined
): linkedCampaignId is string {
  if (!linkedCampaignId) {
    return false;
  }
  return storeCampaignId !== linkedCampaignId;
}

export function applyDiscoveredCampaignToBuilder(campaignId: string, campaign: Campaign): "apply" | "conflict" {
  const state = useJourneyBuilderStore.getState();
  return decideAgentHydrate({
    isDirty: state.isDirty,
    campaignId,
    campaign,
    hydrate: (next) => {
      state.actions.hydrateFromCampaign({
        campaign: next,
        builderPhase: inferBuilderPhase({ previous: state.campaign, next })
      });
      state.actions.setLinkedCampaignId(next.id ?? campaignId);
      state.actions.setDraftPersistence("persisted");
      state.actions.setDirty(false);
    },
    conflict: (id, next) => {
      state.actions.setAgentConflict({ campaignId: id, campaign: next });
    }
  });
}

export function useAgentCampaignSync() {
  const [hydrating, setHydrating] = useState(false);
  const lastHydratedIdRef = useRef<string | null>(null);
  const agentConflict = useJourneyBuilderStore((state) => state.agentConflict);

  useEffect(() => {
    return () => {
      lastHydratedIdRef.current = null;
    };
  }, []);

  const hydrateFromCampaignId = useCallback(async (campaignId: string) => {
    const state = useJourneyBuilderStore.getState();
    if (
      lastHydratedIdRef.current === campaignId &&
      state.campaign?.id === campaignId &&
      state.builderPhase !== "idle"
    ) {
      return;
    }

    setHydrating(true);
    try {
      const result = await getCampaign(campaignId, "draft");
      if (!result.success || !result.data) {
        toast.error(result.error ?? "Could not load the Agent draft.");
        return;
      }

      const decision = applyDiscoveredCampaignToBuilder(campaignId, result.data as Campaign);
      if (decision === "apply") {
        lastHydratedIdRef.current = campaignId;
      }
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Could not load the Agent draft.");
    } finally {
      setHydrating(false);
    }
  }, []);

  const keepLocalChanges = useCallback(() => {
    useJourneyBuilderStore.getState().actions.clearAgentConflict();
  }, []);

  const applyAgentVersion = useCallback(() => {
    const conflict = useJourneyBuilderStore.getState().agentConflict;
    if (!conflict) {
      return;
    }

    const current = useJourneyBuilderStore.getState();
    current.actions.hydrateFromCampaign({
      campaign: conflict.campaign,
      builderPhase: inferBuilderPhase({ previous: current.campaign, next: conflict.campaign })
    });
    current.actions.setLinkedCampaignId(conflict.campaign.id ?? conflict.campaignId);
    current.actions.setDraftPersistence("persisted");
    current.actions.setDirty(false);
    current.actions.clearAgentConflict();
    lastHydratedIdRef.current = conflict.campaign.id ?? conflict.campaignId;
  }, []);

  return {
    hydrateFromCampaignId,
    hydrating,
    agentConflict,
    keepLocalChanges,
    applyAgentVersion
  };
}
