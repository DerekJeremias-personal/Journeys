"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Bot } from "lucide-react";

import { AgentChat } from "@/components/loyalty/agent-chat";
import { TemplatesPicker } from "@/components/loyalty/campaigns/wizard/components/templates-picker";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { isCampaignAgentWritable } from "@/lib/campaign-agent/campaign-mode";
import type { Campaign } from "@/lib/campaign-types";

import { BuilderCanvasSurface } from "./builder-canvas-surface";
import { CampaignHealthSidebar } from "./campaign-health-sidebar";
import { CampaignSyncStatusPill } from "./campaign-sync-status-pill";
import { AgentLinkDraftBanner } from "./agent-link-draft-banner";
import { AgentSyncConflictBanner } from "./agent-sync-conflict-banner";
import { computeCampaignHealth } from "./compute-campaign-health";
import { inferBuilderPhase, type BuilderPhase } from "./infer-builder-phase";
import { JourneyBuilderFormProvider } from "./journey-builder-form-provider";
import {
  selectBuilderPhase,
  selectCampaign,
  selectAgentPanelOpen,
  selectDraftPersistence,
  selectLayoutMode,
  selectLinkedCampaignId,
  selectMapViewModel,
  selectSelectedNodeId,
  useJourneyBuilderActions,
  useJourneyBuilderStore
} from "./journey-builder-store";
import { LinkDraftDialog } from "./link-draft-dialog";
import { NodeStepPanel } from "./node-step-panel";
import { useAgentCampaignSync, shouldAutoFetchLinkedCampaign } from "./use-agent-campaign-sync";
import { useCreateEmptyDraft } from "./use-create-empty-draft";
import { useSaveCampaignDraft } from "./use-save-campaign-draft";
import { WorkspaceConfirmDialog } from "./workspace-confirm-dialog";

const HEALTH_NODE_TO_PHASE: Partial<Record<string, BuilderPhase>> = {
  setup: "setup",
  eligibility: "eligibility",
  criteria: "criteria",
  actions: "actions"
};

export function CampaignJourneyBuilderShell() {
  const layoutMode = useJourneyBuilderStore(selectLayoutMode);
  const builderPhase = useJourneyBuilderStore(selectBuilderPhase);
  const selectedNodeId = useJourneyBuilderStore(selectSelectedNodeId);
  const campaign = useJourneyBuilderStore(selectCampaign);
  const linkedCampaignId = useJourneyBuilderStore(selectLinkedCampaignId);
  const mapViewModel = useJourneyBuilderStore(selectMapViewModel);
  const agentPanelOpen = useJourneyBuilderStore(selectAgentPanelOpen);
  const draftPersistence = useJourneyBuilderStore(selectDraftPersistence);
  const actions = useJourneyBuilderActions();
  const [templateOpen, setTemplateOpen] = useState(false);
  const [confirmNewDraftOpen, setConfirmNewDraftOpen] = useState(false);
  const [confirmNewCampaignOpen, setConfirmNewCampaignOpen] = useState(false);
  const [linkDraftBannerVisible, setLinkDraftBannerVisible] = useState(false);
  const [linkDraftDialogOpen, setLinkDraftDialogOpen] = useState(false);
  const { saveDraft, saving, canSave } = useSaveCampaignDraft();
  const { createEmptyDraft, creating } = useCreateEmptyDraft();
  const { hydrateFromCampaignId, agentConflict, keepLocalChanges, applyAgentVersion } = useAgentCampaignSync();

  useEffect(() => {
    if (!shouldAutoFetchLinkedCampaign(linkedCampaignId, campaign?.id)) {
      return;
    }
    void hydrateFromCampaignId(linkedCampaignId);
  }, [linkedCampaignId, campaign?.id, hydrateFromCampaignId]);

  useEffect(() => {
    if (linkedCampaignId || campaign?.id) {
      setLinkDraftBannerVisible(false);
    }
  }, [linkedCampaignId, campaign?.id]);

  const agentReadOnly = campaign?.status ? !isCampaignAgentWritable(campaign.status) : false;
  const healthReport = useMemo(() => computeCampaignHealth(campaign), [campaign]);
  const showHealthSidebar = builderPhase === "review" && layoutMode !== "focusEdit";

  const handleLinkDraftSelect = useCallback(
    (campaignId: string) => {
      setLinkDraftBannerVisible(false);
      void hydrateFromCampaignId(campaignId);
    },
    [hydrateFromCampaignId]
  );

  const handleTemplateSelect = (initial: Partial<Campaign>) => {
    const nextCampaign: Campaign = {
      name: initial.name ?? "Untitled campaign",
      status: initial.status ?? "draft",
      ...initial
    };
    actions.hydrateFromCampaign({
      campaign: nextCampaign,
      builderPhase: inferBuilderPhase({ previous: null, next: nextCampaign })
    });
    actions.setDraftPersistence("local");
    actions.setLinkedCampaignId(null);
    setTemplateOpen(false);
  };

  const runCreateEmptyDraft = () => {
    void createEmptyDraft();
  };

  const handleStartBlank = () => {
    if (draftPersistence === "persisted") {
      setConfirmNewDraftOpen(true);
      return;
    }
    runCreateEmptyDraft();
  };

  const handleNewCampaign = () => {
    setConfirmNewCampaignOpen(true);
  };

  const confirmNewCampaign = () => {
    actions.reset();
  };

  const handleHealthFix = (nodeId: string) => {
    const phase = HEALTH_NODE_TO_PHASE[nodeId];
    if (phase) {
      actions.setBuilderPhase(phase);
      return;
    }
    const mapNode = [...mapViewModel.coreNodes, ...mapViewModel.branches.flatMap((branch) => branch.seq)].find(
      (node) => node.id === nodeId || node.journeyNodeId === nodeId
    );
    if (mapNode) {
      actions.selectNode(mapNode.id);
    }
  };

  return (
    <JourneyBuilderFormProvider>
      <div className="space-y-4">
        {agentConflict ? (
          <AgentSyncConflictBanner onKeepMine={keepLocalChanges} onUseAgentVersion={applyAgentVersion} />
        ) : null}

        {linkDraftBannerVisible ? (
          <AgentLinkDraftBanner
            onLinkDraft={() => setLinkDraftDialogOpen(true)}
            onDismiss={() => setLinkDraftBannerVisible(false)}
          />
        ) : null}

        {agentReadOnly ? (
          <p className="rounded-lg border border-amber-500/30 bg-amber-500/5 px-3 py-2 text-sm text-amber-900 dark:text-amber-200">
            The Agent is read-only because this linked campaign is not a draft.
          </p>
        ) : null}

        <div className="flex flex-wrap items-center justify-between gap-2">
          <CampaignSyncStatusPill />
          <div className="flex flex-wrap items-center justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => actions.setAgentPanelOpen(!agentPanelOpen)}
            >
              <Bot className="mr-1.5 h-3.5 w-3.5" aria-hidden />
              {agentPanelOpen ? "Hide Agent" : "Show Agent"}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setTemplateOpen(true)}>
              Start from template
            </Button>
            <Button type="button" variant="outline" size="sm" disabled={creating} onClick={handleStartBlank}>
              {creating ? "Creating…" : "Start blank"}
            </Button>
            {builderPhase !== "idle" && builderPhase !== "review" ? (
              <Button type="button" variant="outline" size="sm" onClick={() => actions.setBuilderPhase("review")}>
                Review
              </Button>
            ) : null}
            <Button type="button" size="sm" disabled={!canSave || saving} onClick={() => void saveDraft()}>
              {saving ? "Saving…" : "Save as draft"}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={handleNewCampaign}>
              New campaign
            </Button>
          </div>
        </div>

        <div className="flex min-h-[560px] gap-4 max-lg:flex-col">
          {agentPanelOpen ? (
            <aside className="w-full min-w-[320px] max-w-[360px] shrink-0" aria-label="Agent">
              <AgentChat
                linkedCampaignId={linkedCampaignId ?? campaign?.id ?? null}
                onDiscoveredCampaignId={(id) => void hydrateFromCampaignId(id)}
              />
            </aside>
          ) : null}

          <div className="min-w-0 flex-1">
            {layoutMode === "focusEdit" && selectedNodeId ? (
              <div className="grid h-full min-h-[560px] gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(320px,520px)]">
                <BuilderCanvasSurface />
                <NodeStepPanel />
              </div>
            ) : showHealthSidebar ? (
              <div className="grid h-full min-h-[560px] gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
                <BuilderCanvasSurface />
                <CampaignHealthSidebar report={healthReport} mode="review" onFix={handleHealthFix} />
              </div>
            ) : (
              <BuilderCanvasSurface />
            )}
          </div>
        </div>

        <Dialog open={templateOpen} onOpenChange={setTemplateOpen}>
          <DialogContent className="max-w-3xl">
            <DialogHeader>
              <DialogTitle>Start from template</DialogTitle>
              <DialogDescription>Choose a starter campaign structure to hydrate the journey builder.</DialogDescription>
            </DialogHeader>
            <TemplatesPicker onSelect={handleTemplateSelect} />
          </DialogContent>
        </Dialog>

        <LinkDraftDialog
          open={linkDraftDialogOpen}
          onOpenChange={setLinkDraftDialogOpen}
          onSelect={handleLinkDraftSelect}
        />

        <WorkspaceConfirmDialog
          open={confirmNewDraftOpen}
          onOpenChange={setConfirmNewDraftOpen}
          title="Create new draft?"
          description="Creates a new empty draft you can edit manually or with the Agent. Your current saved draft stays in the campaigns list."
          confirmLabel="Create draft"
          onConfirm={runCreateEmptyDraft}
        />

        <WorkspaceConfirmDialog
          open={confirmNewCampaignOpen}
          onOpenChange={setConfirmNewCampaignOpen}
          title="Start a new campaign?"
          description="Clears the builder. Unsaved local changes will be lost."
          confirmLabel="New campaign"
          onConfirm={confirmNewCampaign}
        />
      </div>
    </JourneyBuilderFormProvider>
  );
}
