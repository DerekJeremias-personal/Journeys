"use client";

import { useMemo } from "react";

import { LoyaltyRuleBuilder } from "@/components/loyalty/promotions";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { Journey, LoyaltyRule, Rule, RuleSet } from "@/lib/campaign-types";

import {
  getCriteriaTargetNode,
  getEligibilityTargetNode,
  summarizeJourneyRules
} from "../journey-builder-journey-utils";
import {
  selectCampaign,
  selectStationRuleDrafts,
  type StationRuleVariant,
  useJourneyBuilderActions,
  useJourneyBuilderStore
} from "../journey-builder-store";

function getTargetNodeForVariant(journey: Journey | null | undefined, variant: StationRuleVariant): Journey | null {
  if (!journey) {
    return null;
  }
  return variant === "eligibility" ? getEligibilityTargetNode(journey) : getCriteriaTargetNode(journey);
}

function upsertDraftRuleSet(existing: RuleSet[] | null | undefined, name: string, rule: Partial<Rule>): RuleSet[] {
  const nextRuleSet: RuleSet = {
    id: crypto.randomUUID(),
    name,
    schemaId: null,
    ruleJsonElement: rule,
    outcomesJsonElement: [],
    tenantId: null
  };
  const rules = existing ?? [];
  const index = rules.findIndex((entry) => entry.name === name);
  if (index >= 0) {
    return rules.map((entry, entryIndex) => (entryIndex === index ? { ...entry, ruleJsonElement: rule } : entry));
  }
  return [...rules, nextRuleSet];
}

export interface CriteriaStationEditorProps {
  readonly variant: StationRuleVariant;
}

export function CriteriaStationEditor({ variant }: CriteriaStationEditorProps) {
  const campaign = useJourneyBuilderStore(selectCampaign);
  const drafts = useJourneyBuilderStore(selectStationRuleDrafts);
  const actions = useJourneyBuilderActions();

  const targetNode = useMemo(() => getTargetNodeForVariant(campaign?.journey, variant), [campaign?.journey, variant]);

  const initialRule = drafts[variant];
  const draftRuleSetName = variant === "eligibility" ? "Eligibility criteria" : "Audience criteria";

  if (!campaign?.journey || !targetNode?.id) {
    return (
      <Alert>
        <AlertDescription>
          The Agent or a template will populate the journey graph before criteria editing is available.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      <Alert>
        <AlertDescription>
          Editing <span className="font-medium">{targetNode.name ?? "journey step"}</span>.{" "}
          {summarizeJourneyRules(targetNode)}
        </AlertDescription>
      </Alert>

      {variant === "eligibility" ? (
        <div className="space-y-2">
          <Label htmlFor="audience-title">Audience title</Label>
          <Input
            id="audience-title"
            value={targetNode.name ?? ""}
            placeholder="e.g. Loyalty members in California"
            onChange={(event) => {
              actions.patchJourneyNode(targetNode.id!, { name: event.target.value });
            }}
          />
        </div>
      ) : null}

      <LoyaltyRuleBuilder
        key={`${variant}-${targetNode.id}-${campaign.id ?? "draft"}`}
        value={(initialRule as LoyaltyRule | undefined) ?? null}
        onChange={(rule) => {
          actions.setStationRuleDraft(variant, rule);
          if (!rule || !targetNode.id) {
            return;
          }
          actions.patchJourneyNode(targetNode.id, {
            rules: upsertDraftRuleSet(targetNode.rules, draftRuleSetName, rule)
          });
        }}
      />
    </div>
  );
}
