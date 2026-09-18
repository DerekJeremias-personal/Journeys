"use client";

import { useMemo, useState } from "react";

import { Button } from "@/components/ui/button";
import type { Campaign } from "@/lib/campaign-types";

import { computeAffectedEstimate } from "./compute-affected-estimate";
import { getActionsTargetNode, getCriteriaTargetNode, getEligibilityTargetNode } from "./journey-builder-journey-utils";
import type { BuilderPhase } from "./infer-builder-phase";
import { labelForOutcomeKind, outcomesFromRuleSet, primaryRuleSet } from "./outcome-utils";

export interface StepSummaryProps {
  readonly phase: BuilderPhase;
  readonly campaign: Campaign | null;
}

export function StepSummary({ phase, campaign }: StepSummaryProps) {
  const [calculating, setCalculating] = useState(false);
  const [estimate, setEstimate] = useState<number | null>(null);

  const stats = useMemo(() => {
    const journey = campaign?.journey;
    if (!journey) {
      return { eligibilityRules: 0, criteriaRules: 0, actionCount: 0, hasEventRule: false };
    }
    const eligibility = getEligibilityTargetNode(journey);
    const criteria = getCriteriaTargetNode(journey);
    const actions = getActionsTargetNode(journey);
    return {
      eligibilityRules: eligibility.rules?.length ?? 0,
      criteriaRules: criteria.rules?.length ?? 0,
      actionCount: outcomesFromRuleSet(primaryRuleSet(actions)).length,
      hasEventRule: (criteria.rules?.length ?? 0) > 0,
    };
  }, [campaign?.journey]);

  const recalc = () => {
    setCalculating(true);
    window.setTimeout(() => {
      setEstimate(
        computeAffectedEstimate({
          eligibilityRuleCount: stats.eligibilityRules,
          criteriaRuleCount: stats.criteriaRules,
          hasEventRule: stats.hasEventRule,
        })
      );
      setCalculating(false);
    }, 800);
  };

  const actionsNode = campaign?.journey ? getActionsTargetNode(campaign.journey) : null;
  const outcomes = outcomesFromRuleSet(primaryRuleSet(actionsNode));

  return (
    <div className="space-y-4 text-sm">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Campaign</p>
        <p className="font-medium">{campaign?.name?.trim() || "Untitled campaign"}</p>
      </div>

      {phase !== "setup" && campaign?.journey ? (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Audience</p>
          <p>{getEligibilityTargetNode(campaign.journey).name || "Not titled yet"}</p>
          <p className="text-muted-foreground">{stats.eligibilityRules} eligibility rule(s)</p>
        </div>
      ) : null}

      {phase === "criteria" || phase === "actions" || phase === "review" ? (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Triggers</p>
          <p className="text-muted-foreground">{stats.criteriaRules} configured</p>
        </div>
      ) : null}

      {phase === "actions" || phase === "review" ? (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Actions</p>
          {outcomes.length === 0 ? (
            <p className="text-muted-foreground">None yet</p>
          ) : (
            <ul className="mt-1 space-y-1">
              {outcomes.map((outcome, index) => (
                <li key={`${outcome.Kind}-${String(index)}`} className="text-muted-foreground">
                  {labelForOutcomeKind(outcome.Kind)}
                </li>
              ))}
            </ul>
          )}
        </div>
      ) : null}

      <div className="rounded-lg border bg-background p-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Affected customers</p>
        <p className="mt-1 text-lg font-semibold">
          {estimate != null ? estimate.toLocaleString() : "—"}
          <span className="ml-1 text-xs font-normal text-muted-foreground">Estimate</span>
        </p>
        <Button type="button" variant="outline" size="sm" className="mt-2" disabled={calculating} onClick={recalc}>
          {calculating ? "Calculating…" : "Recalculate"}
        </Button>
      </div>
    </div>
  );
}
