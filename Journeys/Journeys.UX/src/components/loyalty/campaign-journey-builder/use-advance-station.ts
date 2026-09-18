"use client";

import { useCallback, useEffect } from "react";
import toast from "react-hot-toast";

import { resolveForwardAdvance, type ReviewModalKind } from "./advance-station";
import { ensureProgressiveJourneyScaffold } from "./journey-builder-journey-utils";
import {
  selectBuilderPhase,
  selectCampaign,
  useJourneyBuilderActions,
  useJourneyBuilderStore,
} from "./journey-builder-store";
import { useSaveCampaignDraft } from "./use-save-campaign-draft";
import { validateStationForContinue, validationHint } from "./validate-station";

export function useAdvanceStation(onOpenReviewModal: (modal: ReviewModalKind) => void): {
  validation: ReturnType<typeof validateStationForContinue>;
  continuing: boolean;
  advanceBack: () => void;
  advanceForward: () => Promise<void>;
  completeReviewAdvance: (modal: ReviewModalKind) => Promise<void>;
} {
  const phase = useJourneyBuilderStore(selectBuilderPhase);
  const campaign = useJourneyBuilderStore(selectCampaign);
  const actions = useJourneyBuilderActions();
  const { saveDraft, saving } = useSaveCampaignDraft();

  const validation = validateStationForContinue(phase, campaign);

  useEffect(() => {
    if (useJourneyBuilderStore.getState().canvasPhase !== "traveling") {
      return;
    }
    const timeout = window.setTimeout(() => {
      useJourneyBuilderStore.getState().actions.setCanvasPhase("entering");
      window.setTimeout(() => {
        useJourneyBuilderStore.getState().actions.setCanvasPhase("idle");
      }, 300);
    }, 60);
    return () => window.clearTimeout(timeout);
  }, [phase]);

  const persistIfNeeded = useCallback(async (): Promise<boolean> => {
    const state = useJourneyBuilderStore.getState();
    if (state.draftPersistence !== "persisted" || !state.campaign?.id) {
      return true;
    }
    await saveDraft();
    return !useJourneyBuilderStore.getState().isDirty;
  }, [saveDraft]);

  const applyForwardPhase = useCallback(
    async (nextPhase: ReturnType<typeof selectBuilderPhase>) => {
      const state = useJourneyBuilderStore.getState();
      if (!state.campaign) {
        return;
      }

      const scaffolded = ensureProgressiveJourneyScaffold(state.campaign, nextPhase);
      if (scaffolded !== state.campaign) {
        actions.hydrateFromCampaign({
          campaign: scaffolded,
          builderPhase: state.builderPhase,
        });
      }

      const saved = await persistIfNeeded();
      if (!saved) {
        toast.error("Could not save before continuing.");
        return;
      }

      actions.advanceStation({ direction: "forward", nextPhase });
    },
    [actions, persistIfNeeded]
  );

  const advanceForward = useCallback(async () => {
    const currentPhase = useJourneyBuilderStore.getState().builderPhase;
    const currentCampaign = useJourneyBuilderStore.getState().campaign;
    const currentValidation = validateStationForContinue(currentPhase, currentCampaign);
    const intent = resolveForwardAdvance(currentPhase, currentValidation);

    if (intent.type === "blocked") {
      if (!currentValidation.ok) {
        toast.error(validationHint(currentValidation.reason));
      }
      return;
    }
    if (intent.type === "review_modal") {
      onOpenReviewModal(intent.modal);
      return;
    }
    await applyForwardPhase(intent.nextPhase);
  }, [applyForwardPhase, onOpenReviewModal]);

  const completeReviewAdvance = useCallback(
    async (modal: ReviewModalKind) => {
      const currentPhase = useJourneyBuilderStore.getState().builderPhase;
      const currentCampaign = useJourneyBuilderStore.getState().campaign;
      const currentValidation = validateStationForContinue(currentPhase, currentCampaign);
      const intent = resolveForwardAdvance(currentPhase, currentValidation, { skipReviewModal: true });
      if (intent.type !== "advance") {
        return;
      }
      if (modal === "criteria" && intent.nextPhase !== "actions") {
        return;
      }
      if (modal === "actions" && intent.nextPhase !== "review") {
        return;
      }
      await applyForwardPhase(intent.nextPhase);
    },
    [applyForwardPhase]
  );

  const advanceBack = useCallback(() => {
    actions.advanceStation({ direction: "back" });
  }, [actions]);

  return {
    validation,
    continuing: saving,
    advanceBack,
    advanceForward,
    completeReviewAdvance,
  };
}
