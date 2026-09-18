import type { Campaign, Journey, RuleSet } from "@/lib/campaign-types";

import type { BuilderPhase } from "./infer-builder-phase";
import { getActionsTargetNode, getCriteriaTargetNode, getEligibilityTargetNode } from "./journey-builder-journey-utils";

export type StationValidationFailureReason =
  | "missing_name"
  | "missing_start_date"
  | "missing_eligibility"
  | "missing_criteria"
  | "missing_actions";

export type StationValidationResult = { ok: true } | { ok: false; reason: StationValidationFailureReason };

function hasConfiguredRule(rules: RuleSet[] | null | undefined): boolean {
  if (!rules || rules.length === 0) {
    return false;
  }
  return rules.some((ruleSet) => {
    const rule = ruleSet.ruleJsonElement;
    return Boolean(rule && typeof rule === "object" && Object.keys(rule as object).length > 0);
  });
}

function countOutcomes(node: Journey): number {
  return (
    node.rules?.reduce((total, ruleSet) => {
      const outcomes = ruleSet.outcomesJsonElement;
      if (Array.isArray(outcomes)) {
        return total + outcomes.length;
      }
      if (outcomes && typeof outcomes === "object") {
        return total + 1;
      }
      return total;
    }, 0) ?? 0
  );
}

export function validateStationForContinue(phase: BuilderPhase, campaign: Campaign | null): StationValidationResult {
  if (!campaign) {
    return { ok: false, reason: "missing_name" };
  }

  switch (phase) {
    case "setup": {
      if (!campaign.name?.trim()) {
        return { ok: false, reason: "missing_name" };
      }
      if (!campaign.startDate?.trim()) {
        return { ok: false, reason: "missing_start_date" };
      }
      return { ok: true };
    }
    case "eligibility": {
      if (!campaign.journey) {
        return { ok: false, reason: "missing_eligibility" };
      }
      const node = getEligibilityTargetNode(campaign.journey);
      if (!node.name?.trim()) {
        return { ok: false, reason: "missing_eligibility" };
      }
      if (!hasConfiguredRule(node.rules)) {
        return { ok: false, reason: "missing_eligibility" };
      }
      return { ok: true };
    }
    case "criteria": {
      if (!campaign.journey) {
        return { ok: false, reason: "missing_criteria" };
      }
      const node = getCriteriaTargetNode(campaign.journey);
      if (!hasConfiguredRule(node.rules)) {
        return { ok: false, reason: "missing_criteria" };
      }
      return { ok: true };
    }
    case "actions": {
      if (!campaign.journey) {
        return { ok: false, reason: "missing_actions" };
      }
      const node = getActionsTargetNode(campaign.journey);
      if (countOutcomes(node) === 0) {
        return { ok: false, reason: "missing_actions" };
      }
      return { ok: true };
    }
    case "idle":
    case "review":
      return { ok: true };
    default: {
      const exhaustive: never = phase;
      return exhaustive;
    }
  }
}

export function validationHint(reason: StationValidationFailureReason): string {
  switch (reason) {
    case "missing_name":
      return "Enter a campaign name to continue.";
    case "missing_start_date":
      return "Set a start date to continue.";
    case "missing_eligibility":
      return "Add an audience title and at least one eligibility rule.";
    case "missing_criteria":
      return "Add at least one trigger rule to continue.";
    case "missing_actions":
      return "Add at least one action to continue.";
    default: {
      const exhaustive: never = reason;
      return exhaustive;
    }
  }
}
