import type { Campaign, Journey } from "@/lib/campaign-types";

export type MapNodeKind = "Campaign" | "Eligibility" | "Qualification" | "Outcome";

export interface CampaignMapNode {
  readonly id: string;
  readonly kind: MapNodeKind;
  readonly title: string;
  readonly summary: string | null;
  readonly journeyNodeId: string | null;
  readonly core: boolean;
}

export interface CampaignMapBranch {
  readonly id: string;
  readonly parentId: string;
  readonly seq: readonly CampaignMapNode[];
}

export interface CampaignMapViewModel {
  readonly coreNodes: readonly CampaignMapNode[];
  readonly branches: readonly CampaignMapBranch[];
}

function journeyNodeId(node: Journey, fallback: string): string {
  return node.id ?? fallback;
}

function hasRules(node: Journey): boolean {
  return (node.rules?.length ?? 0) > 0;
}

function hasOutcomes(node: Journey): boolean {
  if (!node.rules) {
    return false;
  }
  return node.rules.some((ruleSet) => {
    const outcomes = ruleSet.outcomesJsonElement;
    return Array.isArray(outcomes) && outcomes.length > 0;
  });
}

function summarizeNode(node: Journey): string | null {
  const ruleCount = node.rules?.length ?? 0;
  if (ruleCount === 0) {
    return null;
  }
  const outcomeCount =
    node.rules?.reduce((acc, ruleSet) => {
      const outcomes = ruleSet.outcomesJsonElement;
      return acc + (Array.isArray(outcomes) ? outcomes.length : 0);
    }, 0) ?? 0;
  if (outcomeCount > 0) {
    return `${String(ruleCount)} rule set${ruleCount === 1 ? "" : "s"} · ${String(outcomeCount)} outcome${outcomeCount === 1 ? "" : "s"}`;
  }
  return `${String(ruleCount)} rule set${ruleCount === 1 ? "" : "s"}`;
}

function kindForPathNode(node: Journey, index: number, pathLength: number): MapNodeKind {
  if (pathLength === 1) {
    if (hasOutcomes(node)) {
      return "Outcome";
    }
    if (hasRules(node)) {
      return "Qualification";
    }
    return "Eligibility";
  }
  if (index === 0) {
    return "Eligibility";
  }
  if (index === pathLength - 1) {
    return "Outcome";
  }
  return "Qualification";
}

function flattenFirstChildPath(root: Journey): Journey[] {
  const path: Journey[] = [root];
  let current: Journey | undefined = root;
  while (current.children && current.children.length > 0) {
    const firstChild: Journey | undefined = current.children[0];
    if (!firstChild) {
      break;
    }
    path.push(firstChild);
    current = firstChild;
  }
  return path;
}

function journeyNodeToMapNode(node: Journey, index: number, pathLength: number, core: boolean): CampaignMapNode {
  const id = journeyNodeId(node, `journey-node-${String(index)}`);
  return {
    id,
    kind: kindForPathNode(node, index, pathLength),
    title: node.name ?? "Untitled step",
    summary: summarizeNode(node),
    journeyNodeId: node.id ?? id,
    core,
  };
}

function pathToBranchSeq(path: Journey[], _parentId: string): readonly CampaignMapNode[] {
  return path.map((node, index) => journeyNodeToMapNode(node, index, path.length, false));
}

function collectBranches(root: Journey, campaignNodeId: string): CampaignMapBranch[] {
  const branches: CampaignMapBranch[] = [];

  const visit = (node: Journey, parentMapId: string): void => {
    const children = node.children ?? [];
    for (let i = 1; i < children.length; i += 1) {
      const child = children[i];
      if (!child) {
        continue;
      }
      const branchRootPath = flattenFirstChildPath(child);
      const branchId = journeyNodeId(child, `branch-${String(i)}`);
      branches.push({
        id: branchId,
        parentId: parentMapId,
        seq: pathToBranchSeq(branchRootPath, campaignNodeId),
      });
      for (const branchNode of branchRootPath) {
        visit(branchNode, branchId);
      }
    }
    const firstChild = children[0];
    if (firstChild) {
      visit(firstChild, parentMapId);
    }
  };

  visit(root, campaignNodeId);
  return branches;
}

export function campaignToMapViewModel(campaign: Campaign): CampaignMapViewModel {
  const campaignNodeId = campaign.id ?? "campaign-root";
  const root: CampaignMapNode = {
    id: campaignNodeId,
    kind: "Campaign",
    title: campaign.name ?? "Untitled Campaign",
    summary: null,
    journeyNodeId: null,
    core: true,
  };

  if (!campaign.journey) {
    return { coreNodes: [root], branches: [] };
  }

  const primaryPath = flattenFirstChildPath(campaign.journey);
  const coreJourneyNodes = primaryPath.map((node, index) =>
    journeyNodeToMapNode(node, index, primaryPath.length, true)
  );

  return {
    coreNodes: [root, ...coreJourneyNodes],
    branches: collectBranches(campaign.journey, campaignNodeId),
  };
}
