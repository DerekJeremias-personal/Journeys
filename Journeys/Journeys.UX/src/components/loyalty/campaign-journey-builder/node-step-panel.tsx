"use client";

import { useMemo } from "react";

import { CriteriaStationEditor } from "./wizard-station-editors/criteria-station-editor";
import { OutcomeListEditor } from "./wizard-station-editors/outcome-list-editor";
import { SetupStationEditor } from "./wizard-station-editors/setup-station-editor";
import type { CampaignMapNode } from "./campaign-to-map-view-model";
import { findJourneyNode, getActionsTargetNode } from "./journey-builder-journey-utils";
import {
  selectBuilderPhase,
  selectCampaign,
  selectMapViewModel,
  selectSelectedNodeId,
  useJourneyBuilderActions,
  useJourneyBuilderStore,
} from "./journey-builder-store";
import { StepPanel } from "./step-panel";
import { StepSummary } from "./step-summary";

function findMapNode(nodes: readonly CampaignMapNode[], nodeId: string): CampaignMapNode | null {
  for (const node of nodes) {
    if (node.id === nodeId) {
      return node;
    }
  }
  return null;
}

function findNodeInViewModel(viewModel: ReturnType<typeof selectMapViewModel>, nodeId: string): CampaignMapNode | null {
  const coreHit = findMapNode(viewModel.coreNodes, nodeId);
  if (coreHit) {
    return coreHit;
  }
  for (const branch of viewModel.branches) {
    const branchHit = findMapNode(branch.seq, nodeId);
    if (branchHit) {
      return branchHit;
    }
  }
  return null;
}

export function NodeStepPanel() {
  const selectedNodeId = useJourneyBuilderStore(selectSelectedNodeId);
  const builderPhase = useJourneyBuilderStore(selectBuilderPhase);
  const mapViewModel = useJourneyBuilderStore(selectMapViewModel);
  const campaign = useJourneyBuilderStore(selectCampaign);
  const actions = useJourneyBuilderActions();

  const node = useMemo(
    () => (selectedNodeId ? findNodeInViewModel(mapViewModel, selectedNodeId) : null),
    [mapViewModel, selectedNodeId]
  );

  const journeyNode = useMemo(() => {
    if (!campaign?.journey || !node?.journeyNodeId) {
      return null;
    }
    return findJourneyNode(campaign.journey, node.journeyNodeId);
  }, [campaign?.journey, node?.journeyNodeId]);

  if (!node) {
    return null;
  }

  const editor =
    node.kind === "Campaign" ? (
      <SetupStationEditor />
    ) : node.kind === "Eligibility" ? (
      <CriteriaStationEditor variant="eligibility" />
    ) : node.kind === "Qualification" ? (
      <CriteriaStationEditor variant="criteria" />
    ) : journeyNode ? (
      <OutcomeListEditor
        targetNode={journeyNode}
        onPatchNode={(patch) => actions.patchJourneyNode(journeyNode.id!, patch)}
      />
    ) : (
      <OutcomeListEditor
        targetNode={campaign?.journey ? getActionsTargetNode(campaign.journey) : { id: "", rules: [] }}
        onPatchNode={(patch) => {
          const target = campaign?.journey ? getActionsTargetNode(campaign.journey) : null;
          if (target?.id) {
            actions.patchJourneyNode(target.id, patch);
          }
        }}
      />
    );

  return (
    <StepPanel
      stepLabel={node.kind}
      title={node.title}
      subtitle={node.summary ?? undefined}
      editor={editor}
      summary={<StepSummary phase={builderPhase} campaign={campaign} />}
      onClose={() => actions.clearSelection()}
    />
  );
}
