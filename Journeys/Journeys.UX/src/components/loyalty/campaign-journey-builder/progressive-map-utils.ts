import type { BuilderPhase } from "./infer-builder-phase";
import type { CampaignMapViewModel } from "./campaign-to-map-view-model";

export function visibleNodeCountForPhase(phase: BuilderPhase): number {
  switch (phase) {
    case "setup":
      return 1;
    case "eligibility":
      return 2;
    case "criteria":
      return 3;
    case "actions":
      return 4;
    case "review":
    case "idle":
      return 4;
    default: {
      const exhaustive: never = phase;
      return exhaustive;
    }
  }
}

export function sliceMapViewModel(viewModel: CampaignMapViewModel, maxNodes: number): CampaignMapViewModel {
  return {
    coreNodes: viewModel.coreNodes.slice(0, Math.max(1, maxNodes)),
    branches: maxNodes >= viewModel.coreNodes.length ? viewModel.branches : [],
  };
}

export function builderPhaseForMapNodeKind(
  kind: CampaignMapViewModel["coreNodes"][number]["kind"]
): BuilderPhase | null {
  switch (kind) {
    case "Campaign":
      return "setup";
    case "Eligibility":
      return "eligibility";
    case "Qualification":
      return "criteria";
    case "Outcome":
      return "actions";
    default:
      return null;
  }
}
