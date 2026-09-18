import type { Campaign, Journey } from "@/lib/campaign-types";

import type { BuilderPhase } from "./infer-builder-phase";

/** Build an empty single-node Journey graph for manual blank starts. */
export function createEmptyJourney(): Journey {
  const id = crypto.randomUUID();
  return {
    id,
    rootNodeId: id,
    name: "Root",
    rules: [],
    children: [],
    navigation: {},
    tenantId: null,
  };
}

export function findJourneyNode(node: Journey, nodeId: string): Journey | null {
  if (node.id === nodeId) {
    return node;
  }
  for (const child of node.children ?? []) {
    const hit = findJourneyNode(child, nodeId);
    if (hit) {
      return hit;
    }
  }
  return null;
}

function mapJourney(node: Journey, transform: (current: Journey) => Journey): Journey {
  const next = transform(node);
  if (!next.children || next.children.length === 0) {
    return next;
  }
  return {
    ...next,
    children: next.children.map((child) => mapJourney(child, transform)),
  };
}

export function patchJourneyNodeInTree(root: Journey, nodeId: string, patch: Partial<Journey>): Journey {
  return mapJourney(root, (node) => (node.id === nodeId ? { ...node, ...patch } : node));
}

/** First child on the main path, falling back to the root node. */
export function getEligibilityTargetNode(journey: Journey): Journey {
  return journey.children?.[0] ?? journey;
}

/** Second node on the main child chain, or eligibility node when only one exists. */
export function getCriteriaTargetNode(journey: Journey): Journey {
  const firstChild = journey.children?.[0];
  if (!firstChild) {
    return journey;
  }
  return firstChild.children?.[0] ?? firstChild;
}

/** Third node on the main child chain, or criteria node when only two exist. */
export function getActionsTargetNode(journey: Journey): Journey {
  const criteria = getCriteriaTargetNode(journey);
  return criteria.children?.[0] ?? criteria;
}

function emptyChildNode(name: string): Journey {
  return {
    id: crypto.randomUUID(),
    name,
    rules: [],
    children: [],
    tenantId: null,
  };
}

/** Ensures main-path journey nodes exist before entering a wizard station. */
export function ensureProgressiveJourneyScaffold(campaign: Campaign, targetPhase: BuilderPhase): Campaign {
  const journey = campaign.journey ?? createEmptyJourney();
  let root = { ...journey };

  if (
    targetPhase === "eligibility" ||
    targetPhase === "criteria" ||
    targetPhase === "actions" ||
    targetPhase === "review"
  ) {
    const children = [...(root.children ?? [])];
    if (children.length === 0) {
      children.push(emptyChildNode("Audience"));
    }
    const eligibility = { ...children[0]! };
    if (targetPhase === "criteria" || targetPhase === "actions" || targetPhase === "review") {
      const critChildren = [...(eligibility.children ?? [])];
      if (critChildren.length === 0) {
        critChildren.push(emptyChildNode("Criteria"));
      }
      const criteria = { ...critChildren[0]! };
      if (targetPhase === "actions" || targetPhase === "review") {
        const actChildren = [...(criteria.children ?? [])];
        if (actChildren.length === 0) {
          actChildren.push(emptyChildNode("Actions"));
        }
        critChildren[0] = { ...criteria, children: actChildren };
      }
      eligibility.children = critChildren;
    }
    children[0] = eligibility;
    root = { ...root, children };
  }

  return { ...campaign, journey: root };
}

export function ensureCampaignJourney(campaign: Campaign): Campaign {
  if (campaign.journey) {
    return campaign;
  }
  return {
    ...campaign,
    journey: createEmptyJourney(),
  };
}

export function summarizeJourneyRules(node: Journey): string {
  const ruleCount = node.rules?.length ?? 0;
  if (ruleCount === 0) {
    return "No rule sets on this step yet.";
  }
  return `${String(ruleCount)} rule set${ruleCount === 1 ? "" : "s"} configured on this journey step.`;
}
