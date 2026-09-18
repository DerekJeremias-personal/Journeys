import type { Campaign, Journey } from "@/lib/campaign-types";

export type BuilderPhase = "idle" | "setup" | "eligibility" | "criteria" | "actions" | "review";

export interface InferBuilderPhaseInput {
  readonly previous: Campaign | null;
  readonly next: Campaign;
  readonly toolNames?: readonly string[];
  readonly workflowPhase?: string | null;
}

const PHASE_ORDER: readonly BuilderPhase[] = ["idle", "setup", "eligibility", "criteria", "actions", "review"];

function phaseIndex(phase: BuilderPhase): number {
  return PHASE_ORDER.indexOf(phase);
}

function maxPhase(a: BuilderPhase, b: BuilderPhase): BuilderPhase {
  return phaseIndex(a) >= phaseIndex(b) ? a : b;
}

function normalizeToken(value: string): string {
  return value.toLowerCase();
}

function phaseFromWorkflow(workflowPhase: string | null | undefined): BuilderPhase | null {
  if (!workflowPhase) {
    return null;
  }
  const token = normalizeToken(workflowPhase);
  if (token.includes("review") || token.includes("validate")) {
    return "review";
  }
  if (token.includes("action") || token.includes("outcome")) {
    return "actions";
  }
  if (token.includes("criteria") || token.includes("qualification")) {
    return "criteria";
  }
  if (token.includes("eligibility") || token.includes("audience")) {
    return "eligibility";
  }
  if (token.includes("setup") || token.includes("create")) {
    return "setup";
  }
  return null;
}

function phaseFromToolNames(toolNames: readonly string[] | undefined): BuilderPhase | null {
  if (!toolNames || toolNames.length === 0) {
    return null;
  }

  let inferred: BuilderPhase = "idle";
  for (const toolName of toolNames) {
    const token = normalizeToken(toolName);
    if (token.includes("validate") || token.includes("review")) {
      inferred = maxPhase(inferred, "review");
      continue;
    }
    if (
      token.includes("outcome") ||
      token.includes("promotion") ||
      token.includes("deposit") ||
      token.includes("points") ||
      token.includes("action")
    ) {
      inferred = maxPhase(inferred, "actions");
      continue;
    }
    if (token.includes("criteria") || token.includes("purchase") || token.includes("event")) {
      inferred = maxPhase(inferred, "criteria");
      continue;
    }
    if (token.includes("eligibility") || token.includes("profile") || token.includes("audience")) {
      inferred = maxPhase(inferred, "eligibility");
      continue;
    }
    if (token.includes("create_campaign") || token.includes("createcampaign") || token === "create") {
      inferred = maxPhase(inferred, "setup");
    }
  }

  return inferred === "idle" ? null : inferred;
}

function countRuleSets(journey: Journey | null | undefined): number {
  if (!journey) {
    return 0;
  }

  let count = journey.rules?.length ?? 0;
  for (const child of journey.children ?? []) {
    count += countRuleSets(child);
  }
  return count;
}

function countOutcomes(journey: Journey | null | undefined): number {
  if (!journey) {
    return 0;
  }

  let count = 0;
  for (const ruleSet of journey.rules ?? []) {
    if (Array.isArray(ruleSet.outcomesJsonElement)) {
      count += ruleSet.outcomesJsonElement.length;
    }
  }
  for (const child of journey.children ?? []) {
    count += countOutcomes(child);
  }
  return count;
}

function phaseFromCampaignDiff(previous: Campaign | null, next: Campaign): BuilderPhase | null {
  if (!previous?.id && next.id) {
    return "setup";
  }

  const previousRules = countRuleSets(previous?.journey);
  const nextRules = countRuleSets(next.journey);
  const previousOutcomes = countOutcomes(previous?.journey);
  const nextOutcomes = countOutcomes(next.journey);

  if (nextOutcomes > previousOutcomes) {
    return "actions";
  }
  if (nextRules > previousRules) {
    return "eligibility";
  }
  if ((next.journey?.children?.length ?? 0) > (previous?.journey?.children?.length ?? 0)) {
    return "criteria";
  }

  return null;
}

function phaseFromCampaignContent(next: Campaign): BuilderPhase | null {
  if (!next.journey) {
    return next.id ? "setup" : null;
  }

  if (countOutcomes(next.journey) > 0) {
    return "actions";
  }
  if (countRuleSets(next.journey) > 0) {
    return "eligibility";
  }
  if ((next.journey.children?.length ?? 0) > 0) {
    return "criteria";
  }
  if (next.id) {
    return "setup";
  }

  return null;
}

export function inferBuilderPhase(input: InferBuilderPhaseInput): BuilderPhase {
  const workflowPhase = phaseFromWorkflow(input.workflowPhase);
  if (workflowPhase) {
    return workflowPhase;
  }

  const toolPhase = phaseFromToolNames(input.toolNames);
  if (toolPhase) {
    return toolPhase;
  }

  const contentPhase = phaseFromCampaignContent(input.next);
  if (contentPhase && contentPhase !== "setup") {
    return contentPhase;
  }

  const diffPhase = phaseFromCampaignDiff(input.previous, input.next);
  if (diffPhase) {
    return diffPhase;
  }

  if (contentPhase) {
    return contentPhase;
  }

  if (input.next.id) {
    return "setup";
  }

  return "idle";
}
