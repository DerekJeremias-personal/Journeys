import { hydrateDecision } from "@/lib/campaign-agent/hydrate-policy";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { applyDiscoveredCampaignToBuilder, decideAgentHydrate, shouldAutoFetchLinkedCampaign } from "./use-agent-campaign-sync";
import { useJourneyBuilderStore } from "./journey-builder-store";

describe("builder hydrate", () => {
  it("conflicts when dirty", () => {
    expect(hydrateDecision(true)).toBe("conflict");
    expect(hydrateDecision(false)).toBe("apply");
  });

  it("does not hydrate over dirty local work", () => {
    const hydrate = vi.fn();
    const conflict = vi.fn();

    expect(
      decideAgentHydrate({
        isDirty: true,
        campaignId: "campaign-1",
        campaign: { id: "campaign-1", status: "draft" },
        hydrate,
        conflict
      })
    ).toBe("conflict");

    expect(hydrate).not.toHaveBeenCalled();
    expect(conflict).toHaveBeenCalledOnce();
  });
});

describe("store hydrate from snapshot", () => {
  beforeEach(() => {
    useJourneyBuilderStore.getState().actions.reset();
  });

  it("does not call hydrateFromCampaign when dirty", () => {
    const actions = useJourneyBuilderStore.getState().actions;
    actions.hydrateFromCampaign({
      campaign: { name: "Local draft", status: "draft" },
      builderPhase: "setup"
    });
    actions.setDirty(true);

    const hydrate = vi.spyOn(useJourneyBuilderStore.getState().actions, "hydrateFromCampaign");

    expect(
      applyDiscoveredCampaignToBuilder("agent-1", {
        id: "agent-1",
        name: "Agent snapshot",
        status: "draft"
      })
    ).toBe("conflict");

    expect(hydrate).not.toHaveBeenCalled();
    expect(useJourneyBuilderStore.getState().campaign?.name).toBe("Local draft");
    expect(useJourneyBuilderStore.getState().isDirty).toBe(true);
  });

  it("increments hydrateGeneration when applying a same-id same-etag snapshot", () => {
    const actions = useJourneyBuilderStore.getState().actions;
    const local = { id: "campaign-1", etag: "etag-1", name: "Local name", status: "draft" };
    actions.hydrateFromCampaign({ campaign: local, builderPhase: "setup" });
    const generation = useJourneyBuilderStore.getState().hydrateGeneration;

    actions.hydrateFromCampaign({
      campaign: { ...local, name: "Agent name" },
      builderPhase: "setup"
    });

    const state = useJourneyBuilderStore.getState();
    expect(state.hydrateGeneration).toBeGreaterThan(generation);
    expect(state.campaign?.name).toBe("Agent name");
  });
});

describe("shouldAutoFetchLinkedCampaign", () => {
  it("does not GET when the store already holds that campaign", () => {
    expect(shouldAutoFetchLinkedCampaign("draft-1", "draft-1")).toBe(false);
  });

  it("does not GET when nothing is linked", () => {
    expect(shouldAutoFetchLinkedCampaign(null, "draft-1")).toBe(false);
  });

  it("GETs when the linked id is not the campaign already in the store", () => {
    expect(shouldAutoFetchLinkedCampaign("draft-2", "draft-1")).toBe(true);
    expect(shouldAutoFetchLinkedCampaign("draft-2", null)).toBe(true);
  });
});

