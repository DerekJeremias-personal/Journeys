import { create } from "zustand";

import type { Campaign, Journey, LoyaltySchema, Rule } from "@/lib/campaign-types";

import { campaignToMapViewModel, type CampaignMapViewModel } from "./campaign-to-map-view-model";
import type { BuilderPhase } from "./infer-builder-phase";
import { previousBuilderPhase } from "./advance-station";
import { ensureCampaignJourney, patchJourneyNodeInTree } from "./journey-builder-journey-utils";

export type StationRuleVariant = "eligibility" | "criteria";

export type AgentConflictState = {
  readonly campaignId: string;
  readonly campaign: Campaign;
} | null;

export type LayoutMode = "discover" | "stream" | "focusEdit";
export type CanvasPhase = "idle" | "traveling" | "entering" | "exiting";
export type DraftPersistence = "none" | "local" | "persisted";

export interface HydrateFromCampaignInput {
  readonly campaign: Campaign;
  readonly builderPhase: BuilderPhase;
  readonly markDirty?: boolean;
}

export interface JourneyBuilderActions {
  hydrateFromCampaign: (input: HydrateFromCampaignInput) => void;
  patchCampaignScalars: (patch: Partial<Campaign>) => void;
  patchJourneyNode: (nodeId: string, patch: Partial<Journey>) => void;
  setStationRuleDraft: (variant: StationRuleVariant, rule: Partial<Rule> | null) => void;
  setSelectedSchema: (schema: LoyaltySchema | null) => void;
  setLayoutMode: (mode: LayoutMode) => void;
  setBuilderPhase: (phase: BuilderPhase) => void;
  advanceStation: (input: { direction: "forward" | "back"; nextPhase?: BuilderPhase }) => void;
  setCanvasPhase: (phase: CanvasPhase) => void;
  selectNode: (nodeId: string) => void;
  clearSelection: () => void;
  setDirty: (dirty: boolean) => void;
  setLinkedCampaignId: (campaignId: string | null) => void;
  setAgentConflict: (conflict: NonNullable<AgentConflictState>) => void;
  clearAgentConflict: () => void;
  setAgentPanelOpen: (open: boolean) => void;
  setDraftPersistence: (mode: DraftPersistence) => void;
  clearAgentCampaignLink: () => void;
  reset: () => void;
}

export interface JourneyBuilderState {
  campaign: Campaign | null;
  mapViewModel: CampaignMapViewModel;
  builderPhase: BuilderPhase;
  layoutMode: LayoutMode;
  selectedNodeId: string | null;
  activeStationIndex: number;
  canvasPhase: CanvasPhase;
  isDirty: boolean;
  linkedCampaignId: string | null;
  selectedSchema: LoyaltySchema | null;
  stationRuleDrafts: Partial<Record<StationRuleVariant, Partial<Rule>>>;
  agentConflict: AgentConflictState;
  agentPanelOpen: boolean;
  draftPersistence: DraftPersistence;
  hydrateGeneration: number;
  actions: JourneyBuilderActions;
}

const EMPTY_MAP_VIEW_MODEL: CampaignMapViewModel = {
  coreNodes: [],
  branches: []
};

const INITIAL_STATE: Omit<JourneyBuilderState, "actions"> = {
  campaign: null,
  mapViewModel: EMPTY_MAP_VIEW_MODEL,
  builderPhase: "idle",
  layoutMode: "discover",
  selectedNodeId: null,
  activeStationIndex: -1,
  canvasPhase: "idle",
  isDirty: false,
  linkedCampaignId: null,
  selectedSchema: null,
  stationRuleDrafts: {},
  agentConflict: null,
  agentPanelOpen: true,
  draftPersistence: "none",
  hydrateGeneration: 0
};

export function builderPhaseToStationIndex(phase: BuilderPhase): number {
  switch (phase) {
    case "idle":
      return -1;
    case "setup":
      return 0;
    case "eligibility":
      return 1;
    case "criteria":
      return 2;
    case "actions":
      return 3;
    case "review":
      return 4;
    default: {
      const exhaustive: never = phase;
      return exhaustive;
    }
  }
}

export const useJourneyBuilderStore = create<JourneyBuilderState>((set) => ({
  ...INITIAL_STATE,
  actions: {
    hydrateFromCampaign: ({ campaign, builderPhase, markDirty = false }) =>
      set((state) => {
        const withJourney = ensureCampaignJourney(campaign);
        return {
          campaign: withJourney,
          mapViewModel: campaignToMapViewModel(withJourney),
          builderPhase,
          activeStationIndex: builderPhaseToStationIndex(builderPhase),
          linkedCampaignId: withJourney.id ?? state.linkedCampaignId,
          isDirty: markDirty ? true : state.isDirty,
          hydrateGeneration: state.hydrateGeneration + 1
        };
      }),
    patchCampaignScalars: (patch) =>
      set((state) => {
        if (!state.campaign) {
          return state;
        }
        const next = ensureCampaignJourney({ ...state.campaign, ...patch });
        return {
          campaign: next,
          mapViewModel: campaignToMapViewModel(next),
          isDirty: true
        };
      }),
    patchJourneyNode: (nodeId, nodePatch) =>
      set((state) => {
        if (!state.campaign?.journey) {
          return state;
        }
        const nextJourney = patchJourneyNodeInTree(state.campaign.journey, nodeId, nodePatch);
        const nextCampaign = { ...state.campaign, journey: nextJourney };
        return {
          campaign: nextCampaign,
          mapViewModel: campaignToMapViewModel(nextCampaign),
          isDirty: true
        };
      }),
    setStationRuleDraft: (variant, rule) =>
      set((state) => ({
        stationRuleDrafts: {
          ...state.stationRuleDrafts,
          [variant]: rule ?? undefined
        },
        isDirty: true
      })),
    setSelectedSchema: (selectedSchema) => set({ selectedSchema }),
    setLayoutMode: (layoutMode) => set({ layoutMode }),
    setBuilderPhase: (builderPhase) =>
      set({
        builderPhase,
        activeStationIndex: builderPhaseToStationIndex(builderPhase)
      }),
    advanceStation: ({ direction, nextPhase }) =>
      set((state) => {
        if (direction === "back") {
          const previous = previousBuilderPhase(state.builderPhase);
          if (!previous) {
            return state;
          }
          return {
            builderPhase: previous,
            activeStationIndex: builderPhaseToStationIndex(previous),
            canvasPhase: "traveling" as const
          };
        }
        if (!nextPhase) {
          return state;
        }
        return {
          builderPhase: nextPhase,
          activeStationIndex: builderPhaseToStationIndex(nextPhase),
          canvasPhase: "traveling" as const
        };
      }),
    setCanvasPhase: (canvasPhase) => set({ canvasPhase }),
    selectNode: (nodeId) =>
      set({
        selectedNodeId: nodeId,
        layoutMode: "focusEdit"
      }),
    clearSelection: () =>
      set({
        selectedNodeId: null,
        layoutMode: "discover"
      }),
    setDirty: (isDirty) => set({ isDirty }),
    setLinkedCampaignId: (linkedCampaignId) => set({ linkedCampaignId }),
    setAgentConflict: (agentConflict) => set({ agentConflict }),
    clearAgentConflict: () => set({ agentConflict: null }),
    setAgentPanelOpen: (agentPanelOpen) => set({ agentPanelOpen }),
    setDraftPersistence: (draftPersistence) => set({ draftPersistence }),
    clearAgentCampaignLink: () =>
      set((state) => ({
        campaign: null,
        mapViewModel: EMPTY_MAP_VIEW_MODEL,
        builderPhase: "idle",
        activeStationIndex: -1,
        layoutMode: state.layoutMode === "focusEdit" ? "discover" : state.layoutMode,
        selectedNodeId: null,
        linkedCampaignId: null,
        draftPersistence: "none",
        isDirty: false,
        agentConflict: null,
        hydrateGeneration: state.hydrateGeneration + 1
      })),
    reset: () =>
      set((state) => ({
        ...INITIAL_STATE,
        hydrateGeneration: state.hydrateGeneration + 1,
        actions: state.actions
      }))
  }
}));

export const selectCampaign = (state: JourneyBuilderState): Campaign | null => state.campaign;
export const selectMapViewModel = (state: JourneyBuilderState): CampaignMapViewModel => state.mapViewModel;
export const selectBuilderPhase = (state: JourneyBuilderState): BuilderPhase => state.builderPhase;
export const selectLayoutMode = (state: JourneyBuilderState): LayoutMode => state.layoutMode;
export const selectSelectedNodeId = (state: JourneyBuilderState): string | null => state.selectedNodeId;
export const selectActiveStationIndex = (state: JourneyBuilderState): number => state.activeStationIndex;
export const selectCanvasPhase = (state: JourneyBuilderState): CanvasPhase => state.canvasPhase;
export const selectSelectedSchema = (state: JourneyBuilderState): LoyaltySchema | null => state.selectedSchema;
export const selectStationRuleDrafts = (
  state: JourneyBuilderState
): Partial<Record<StationRuleVariant, Partial<Rule>>> => state.stationRuleDrafts;
export const selectAgentConflict = (state: JourneyBuilderState): AgentConflictState => state.agentConflict;
export const selectLinkedCampaignId = (state: JourneyBuilderState): string | null => state.linkedCampaignId;
export const selectAgentPanelOpen = (state: JourneyBuilderState): boolean => state.agentPanelOpen;
export const selectDraftPersistence = (state: JourneyBuilderState): DraftPersistence => state.draftPersistence;
export const selectHydrateGeneration = (state: JourneyBuilderState): number => state.hydrateGeneration;
export const selectIsDirty = (state: JourneyBuilderState): boolean => state.isDirty;

export const useJourneyBuilderActions = (): JourneyBuilderActions => useJourneyBuilderStore((state) => state.actions);
