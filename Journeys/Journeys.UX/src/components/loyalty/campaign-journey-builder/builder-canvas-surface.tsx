"use client";

import { useCallback, useEffect, useState } from "react";

import type { Campaign } from "@/lib/campaign-types";

import type { ReviewModalKind } from "./advance-station";
import { ActionsReviewModal } from "./actions-review-modal";
import { CampaignMapCanvas } from "./campaign-map-canvas";
import { CriteriaReviewModal } from "./criteria-review-modal";
import { JourneyCanvas } from "./journey-canvas";
import { JOURNEY_BUILDER_STEPS } from "./journey-builder-steps";
import { ProgressiveMapCanvas } from "./progressive-map-canvas";
import {
  selectActiveStationIndex,
  selectBuilderPhase,
  selectCampaign,
  selectCanvasPhase,
  selectDraftPersistence,
  selectLayoutMode,
  selectMapViewModel,
  selectSelectedNodeId,
  useJourneyBuilderActions,
  useJourneyBuilderStore,
} from "./journey-builder-store";
import { renderJourneyStationEditor } from "./station-editor-slot";
import { StationFooter } from "./station-footer";
import { StepSummary } from "./step-summary";
import { useAdvanceStation } from "./use-advance-station";
import { WelcomeCanvas } from "./welcome-canvas";

function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    if (typeof window.matchMedia !== "function") {
      return;
    }
    const media = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = (): void => setReduced(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  return reduced;
}

function buildHeadings(campaign: Campaign | null) {
  const name = campaign?.name?.trim();
  return JOURNEY_BUILDER_STEPS.map((step) => {
    switch (step.key) {
      case "setup":
        return {
          title: step.title,
          subtitle: step.description,
          mini: name || "Not started",
        };
      case "eligibility":
        return {
          title: step.title,
          subtitle: step.description,
          mini: campaign?.journey?.children?.[0]?.name?.trim() || "Define audience",
        };
      case "criteria":
        return {
          title: step.title,
          subtitle: step.description,
          mini: campaign?.journey ? "Purchase & event rules" : "No criteria yet",
        };
      case "actions":
        return {
          title: step.title,
          subtitle: step.description,
          mini: campaign?.journey ? "Outcomes configured" : "No actions yet",
        };
      case "review":
        return {
          title: step.title,
          subtitle: step.description,
          mini: "Generate campaign path",
        };
      default: {
        const exhaustive: never = step.key;
        return exhaustive;
      }
    }
  });
}

export function BuilderCanvasSurface() {
  const builderPhase = useJourneyBuilderStore(selectBuilderPhase);
  const activeStationIndex = useJourneyBuilderStore(selectActiveStationIndex);
  const canvasPhase = useJourneyBuilderStore(selectCanvasPhase);
  const layoutMode = useJourneyBuilderStore(selectLayoutMode);
  const mapViewModel = useJourneyBuilderStore(selectMapViewModel);
  const selectedNodeId = useJourneyBuilderStore(selectSelectedNodeId);
  const campaign = useJourneyBuilderStore(selectCampaign);
  const draftPersistence = useJourneyBuilderStore(selectDraftPersistence);
  const actions = useJourneyBuilderActions();
  const prefersReducedMotion = usePrefersReducedMotion();
  const [reviewModal, setReviewModal] = useState<ReviewModalKind | null>(null);

  const { validation, continuing, advanceBack, advanceForward, completeReviewAdvance } = useAdvanceStation(
    useCallback((modal) => setReviewModal(modal), [])
  );

  const showMapSurface = builderPhase === "review" || layoutMode === "focusEdit";
  const showJourneySurface =
    (builderPhase !== "idle" || Boolean(campaign?.journey || campaign?.id)) &&
    builderPhase !== "review" &&
    layoutMode !== "focusEdit";
  const showProgressiveMap =
    showJourneySurface && builderPhase !== "idle" && mapViewModel.coreNodes.length > 0;
  const transitionClass = prefersReducedMotion ? "" : "transition-opacity duration-300";

  if (builderPhase === "idle" && !campaign?.id && !campaign?.journey) {
    return <WelcomeCanvas />;
  }

  if (layoutMode === "focusEdit") {
    return (
      <CampaignMapCanvas
        viewModel={mapViewModel}
        selectedNodeId={selectedNodeId}
        onSelectNode={(nodeId) => actions.selectNode(nodeId)}
        onBackgroundClick={() => actions.clearSelection()}
      />
    );
  }

  if (builderPhase === "review" && !showJourneySurface) {
    return (
      <CampaignMapCanvas
        viewModel={mapViewModel}
        selectedNodeId={selectedNodeId}
        onSelectNode={(nodeId) => actions.selectNode(nodeId)}
        onBackgroundClick={() => actions.clearSelection()}
      />
    );
  }

  return (
    <>
      <div className={`grid gap-4 ${showProgressiveMap ? "xl:grid-cols-[minmax(0,1fr)_280px]" : ""}`}>
        <div className="relative min-h-[520px]">
          <div
            className={`${transitionClass} ${showJourneySurface ? "opacity-100" : "pointer-events-none absolute inset-0 opacity-0"}`}
          >
            <div className={`grid gap-4 ${showProgressiveMap ? "lg:grid-cols-[minmax(0,1fr)_240px]" : ""}`}>
              <JourneyCanvas
                activeIndex={Math.max(activeStationIndex, 0)}
                canvasPhase={canvasPhase}
                headings={buildHeadings(campaign)}
                renderStationEditor={renderJourneyStationEditor}
                renderSummary={<StepSummary phase={builderPhase} campaign={campaign} />}
                renderFooter={
                  <StationFooter
                    phase={builderPhase}
                    validation={validation}
                    draftPersistence={draftPersistence}
                    onBack={advanceBack}
                    onContinue={() => void advanceForward()}
                    continuing={continuing}
                  />
                }
                onJumpBack={(index) => {
                  const step = JOURNEY_BUILDER_STEPS[index];
                  if (step) {
                    actions.setBuilderPhase(step.key);
                  }
                }}
              />
              {showProgressiveMap ? (
                <ProgressiveMapCanvas
                  viewModel={mapViewModel}
                  builderPhase={builderPhase}
                  activeStationIndex={Math.max(activeStationIndex, 0)}
                  selectedNodeId={selectedNodeId}
                  onSelectNode={(nodeId) => actions.selectNode(nodeId)}
                  onJumpToPhase={(phase) => actions.setBuilderPhase(phase)}
                />
              ) : null}
            </div>
          </div>

          <div
            className={`${transitionClass} ${showMapSurface ? "opacity-100" : "pointer-events-none absolute inset-0 opacity-0"}`}
          >
            <CampaignMapCanvas
              viewModel={mapViewModel}
              selectedNodeId={selectedNodeId}
              onSelectNode={(nodeId) => actions.selectNode(nodeId)}
              onBackgroundClick={() => actions.clearSelection()}
            />
          </div>
        </div>

        {showProgressiveMap ? (
          <aside className="hidden xl:block">
            <StepSummary phase={builderPhase} campaign={campaign} />
          </aside>
        ) : null}
      </div>

      <CriteriaReviewModal
        open={reviewModal === "criteria"}
        campaign={campaign}
        onOpenChange={(open) => {
          if (!open) {
            setReviewModal(null);
          }
        }}
        onContinue={() => void completeReviewAdvance("criteria").then(() => setReviewModal(null))}
      />
      <ActionsReviewModal
        open={reviewModal === "actions"}
        campaign={campaign}
        onOpenChange={(open) => {
          if (!open) {
            setReviewModal(null);
          }
        }}
        onContinue={() => void completeReviewAdvance("actions").then(() => setReviewModal(null))}
      />
    </>
  );
}
