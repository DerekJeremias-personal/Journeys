"use client";

import { CampaignMapCanvas } from "./campaign-map-canvas";
import type { CampaignMapViewModel } from "./campaign-to-map-view-model";
import type { BuilderPhase } from "./infer-builder-phase";
import { builderPhaseForMapNodeKind, sliceMapViewModel, visibleNodeCountForPhase } from "./progressive-map-utils";

export interface ProgressiveMapCanvasProps {
  readonly viewModel: CampaignMapViewModel;
  readonly builderPhase: BuilderPhase;
  readonly activeStationIndex: number;
  readonly selectedNodeId: string | null;
  readonly onSelectNode: (nodeId: string) => void;
  readonly onJumpToPhase: (phase: BuilderPhase) => void;
}

export function ProgressiveMapCanvas({
  viewModel,
  builderPhase,
  activeStationIndex,
  selectedNodeId,
  onSelectNode,
  onJumpToPhase,
}: ProgressiveMapCanvasProps) {
  const maxNodes = visibleNodeCountForPhase(builderPhase);
  const sliced = sliceMapViewModel(viewModel, maxNodes);

  return (
    <div className="rounded-xl border bg-muted/5 p-4">
      <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Campaign path preview</p>
      <CampaignMapCanvas
        viewModel={sliced}
        selectedNodeId={selectedNodeId}
        panTarget={24}
        onSelectNode={(nodeId) => {
          const node = sliced.coreNodes.find((entry) => entry.id === nodeId);
          const nodeIndex = sliced.coreNodes.findIndex((entry) => entry.id === nodeId);
          if (node && nodeIndex >= 0 && nodeIndex < activeStationIndex) {
            const phase = builderPhaseForMapNodeKind(node.kind);
            if (phase) {
              onJumpToPhase(phase);
              return;
            }
          }
          onSelectNode(nodeId);
        }}
      />
    </div>
  );
}
