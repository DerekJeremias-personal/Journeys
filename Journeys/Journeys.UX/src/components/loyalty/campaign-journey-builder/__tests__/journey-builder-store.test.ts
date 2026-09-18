import { describe, expect, it, beforeEach } from "vitest";

import type { Campaign } from "@/lib/campaign-types";

import { builderPhaseToStationIndex, useJourneyBuilderStore } from "../journey-builder-store";

function campaign(overrides: Partial<Campaign> = {}): Campaign {
  return {
    name: "Test campaign",
    status: "draft",
    id: "campaign-1",
    ...overrides,
  };
}

describe("useJourneyBuilderStore", () => {
  beforeEach(() => {
    useJourneyBuilderStore.getState().actions.reset();
  });

  it("hydrates campaign and map view model", () => {
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign({
        journey: {
          id: "journey-1",
          name: "Root journey",
          rules: [],
          children: [],
        },
      }),
      builderPhase: "setup",
    });

    const state = useJourneyBuilderStore.getState();
    expect(state.campaign?.name).toBe("Test campaign");
    expect(state.mapViewModel.coreNodes[0]?.kind).toBe("Campaign");
    expect(state.builderPhase).toBe("setup");
    expect(state.activeStationIndex).toBe(0);
  });

  it("enters focus edit when selecting a node", () => {
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign(),
      builderPhase: "review",
    });
    useJourneyBuilderStore.getState().actions.selectNode("campaign-1");

    const state = useJourneyBuilderStore.getState();
    expect(state.selectedNodeId).toBe("campaign-1");
    expect(state.layoutMode).toBe("focusEdit");
  });

  it("clears focus edit when selection is cleared", () => {
    useJourneyBuilderStore.getState().actions.selectNode("campaign-1");
    useJourneyBuilderStore.getState().actions.clearSelection();

    const state = useJourneyBuilderStore.getState();
    expect(state.selectedNodeId).toBeNull();
    expect(state.layoutMode).toBe("discover");
  });

  it("patches scalar campaign fields and marks dirty", () => {
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign({ name: "Before" }),
      builderPhase: "setup",
    });

    useJourneyBuilderStore.getState().actions.patchCampaignScalars({ name: "After" });

    const state = useJourneyBuilderStore.getState();
    expect(state.campaign?.name).toBe("After");
    expect(state.isDirty).toBe(true);
  });

  it("patches a journey node and recomputes map view model", () => {
    const journeyId = "journey-1";
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign({
        journey: {
          id: journeyId,
          name: "Root",
          rules: [],
          children: [],
        },
      }),
      builderPhase: "eligibility",
    });

    useJourneyBuilderStore.getState().actions.patchJourneyNode(journeyId, { name: "Updated root" });

    const state = useJourneyBuilderStore.getState();
    expect(state.campaign?.journey?.name).toBe("Updated root");
    expect(state.isDirty).toBe(true);
    expect(state.mapViewModel.coreNodes.length).toBeGreaterThan(0);
  });

  it("stores and clears agent sync conflicts", () => {
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign({ name: "Local draft" }),
      builderPhase: "setup",
      markDirty: true,
    });

    useJourneyBuilderStore.getState().actions.setAgentConflict({
      campaignId: "campaign-1",
      campaign: campaign({ id: "campaign-1", name: "Agent draft" }),
    });

    expect(useJourneyBuilderStore.getState().agentConflict?.campaign.name).toBe("Agent draft");
    useJourneyBuilderStore.getState().actions.clearAgentConflict();
    expect(useJourneyBuilderStore.getState().agentConflict).toBeNull();
  });
  it("advances forward and back", () => {
    useJourneyBuilderStore.getState().actions.hydrateFromCampaign({
      campaign: campaign(),
      builderPhase: "setup",
    });

    useJourneyBuilderStore.getState().actions.advanceStation({
      direction: "forward",
      nextPhase: "eligibility",
    });
    expect(useJourneyBuilderStore.getState().builderPhase).toBe("eligibility");

    useJourneyBuilderStore.getState().actions.advanceStation({ direction: "back" });
    expect(useJourneyBuilderStore.getState().builderPhase).toBe("setup");
  });
});

describe("builderPhaseToStationIndex", () => {
  it("maps builder phases to station indexes", () => {
    expect(builderPhaseToStationIndex("idle")).toBe(-1);
    expect(builderPhaseToStationIndex("setup")).toBe(0);
    expect(builderPhaseToStationIndex("eligibility")).toBe(1);
    expect(builderPhaseToStationIndex("review")).toBe(4);
  });
});

describe("workspace state", () => {
  beforeEach(() => {
    useJourneyBuilderStore.getState().actions.reset();
  });

  it("defaults agentPanelOpen true and draftPersistence none", () => {
    const state = useJourneyBuilderStore.getState();
    expect(state.agentPanelOpen).toBe(true);
    expect(state.draftPersistence).toBe("none");
  });

  it("setAgentPanelOpen toggles visibility", () => {
    useJourneyBuilderStore.getState().actions.setAgentPanelOpen(false);
    expect(useJourneyBuilderStore.getState().agentPanelOpen).toBe(false);
  });

  it("setDraftPersistence updates mode", () => {
    useJourneyBuilderStore.getState().actions.setDraftPersistence("local");
    expect(useJourneyBuilderStore.getState().draftPersistence).toBe("local");
  });

  it("reset restores workspace defaults", () => {
    useJourneyBuilderStore.getState().actions.setAgentPanelOpen(false);
    useJourneyBuilderStore.getState().actions.setDraftPersistence("persisted");
    useJourneyBuilderStore.getState().actions.reset();
    const state = useJourneyBuilderStore.getState();
    expect(state.agentPanelOpen).toBe(true);
    expect(state.draftPersistence).toBe("none");
    expect(state.builderPhase).toBe("idle");
  });
});
