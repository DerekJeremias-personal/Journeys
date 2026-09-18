import type { Outcome, RuleSet } from "@/lib/campaign-types";

export function outcomesFromRuleSet(ruleSet: RuleSet | null | undefined): Outcome[] {
  if (!ruleSet?.outcomesJsonElement) {
    return [];
  }
  if (Array.isArray(ruleSet.outcomesJsonElement)) {
    return ruleSet.outcomesJsonElement as Outcome[];
  }
  if (typeof ruleSet.outcomesJsonElement === "object") {
    return [ruleSet.outcomesJsonElement as Outcome];
  }
  return [];
}

export function labelForOutcomeKind(kind: Outcome["Kind"]): string {
  switch (kind) {
    case "DepositPointsOutcome":
      return "Deposit points";
    case "SpendPointsOutcome":
      return "Spend points";
    case "ExpirePointsOutcome":
      return "Expire points";
    case "TagOutcome":
      return "Tag";
    default:
      return kind;
  }
}

export function primaryRuleSet(node: { rules?: RuleSet[] | null } | null | undefined): RuleSet | null {
  return node?.rules?.[0] ?? null;
}

export function upsertPrimaryRuleSetOutcomes(node: { rules?: RuleSet[] | null }, outcomes: Outcome[]): RuleSet[] {
  const existing = node.rules?.[0];
  const nextRuleSet: RuleSet = {
    id: existing?.id ?? crypto.randomUUID(),
    name: existing?.name ?? "Actions",
    schemaId: existing?.schemaId ?? null,
    ruleJsonElement: existing?.ruleJsonElement ?? {},
    outcomesJsonElement: outcomes,
    tenantId: existing?.tenantId ?? null,
  };
  const rest = node.rules?.slice(1) ?? [];
  return [nextRuleSet, ...rest];
}
