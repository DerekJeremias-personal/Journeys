import type { Campaign } from "@/lib/campaign-types";

export type CampaignHealthIssueKind = "error" | "warning" | "rec";

export interface CampaignHealthIssue {
  readonly id: string;
  readonly kind: CampaignHealthIssueKind;
  readonly title: string;
  readonly why: string;
  readonly fix: string;
  readonly nodeId?: string;
  readonly fixLabel?: string;
}

export interface CampaignHealthReport {
  readonly errors: readonly CampaignHealthIssue[];
  readonly warnings: readonly CampaignHealthIssue[];
  readonly recs: readonly CampaignHealthIssue[];
}

function countOutcomeRuleSets(campaign: Campaign): number {
  const journey = campaign.journey;
  if (!journey) {
    return 0;
  }

  const walk = (node: typeof journey): number => {
    let count = 0;
    for (const ruleSet of node.rules ?? []) {
      const outcomes = ruleSet.outcomesJsonElement;
      if (Array.isArray(outcomes) && outcomes.length > 0) {
        count += 1;
      }
    }
    for (const child of node.children ?? []) {
      count += walk(child);
    }
    return count;
  };

  return walk(journey);
}

/** Client-side campaign health checks ported from the journey builder mockup. */
export function computeCampaignHealth(campaign: Campaign | null): CampaignHealthReport {
  const errors: CampaignHealthIssue[] = [];
  const warnings: CampaignHealthIssue[] = [];
  const recs: CampaignHealthIssue[] = [];

  if (!campaign) {
    return { errors, warnings, recs };
  }

  if (!campaign.name?.trim()) {
    warnings.push({
      id: "missing-name",
      kind: "warning",
      title: "Campaign name is missing",
      why: "Draft campaigns should have a recognizable name before launch.",
      fix: "Add a campaign name in Setup.",
      nodeId: "setup",
      fixLabel: "Edit Setup",
    });
  }

  if (!campaign.endDate) {
    warnings.push({
      id: "no-end-date",
      kind: "warning",
      title: "This campaign has no end date",
      why: "Your campaign will continue running until manually stopped.",
      fix: "Add an end date or confirm the campaign should run indefinitely.",
      nodeId: "setup",
      fixLabel: "Add end date",
    });
  }

  if (!campaign.startDate) {
    warnings.push({
      id: "missing-start-date",
      kind: "warning",
      title: "Start date is not set",
      why: "Campaigns need a schedule before they can go live.",
      fix: "Pick a start date in Setup.",
      nodeId: "setup",
      fixLabel: "Edit Setup",
    });
  }

  if ((campaign.events?.length ?? 0) === 0) {
    recs.push({
      id: "missing-events",
      kind: "rec",
      title: "Pick event types for this campaign",
      why: "Event schemas drive purchase and event criteria in later steps.",
      fix: "Select at least one event type in Setup.",
      nodeId: "setup",
      fixLabel: "Edit Setup",
    });
  }

  if (countOutcomeRuleSets(campaign) === 0) {
    recs.push({
      id: "missing-outcomes",
      kind: "rec",
      title: "Add resulting actions",
      why: "Customers need points, messages, or profile updates when they qualify.",
      fix: "Configure outcomes on a journey step or ask the Agent to add actions.",
      nodeId: "actions",
      fixLabel: "Review actions",
    });
  }

  return { errors, warnings, recs };
}
