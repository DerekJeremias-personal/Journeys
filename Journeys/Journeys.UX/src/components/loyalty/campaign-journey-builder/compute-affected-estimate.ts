export function computeAffectedEstimate(input: {
  eligibilityRuleCount: number;
  criteriaRuleCount: number;
  hasEventRule: boolean;
}): number {
  const base = 24000;
  const profileFactor = Math.pow(0.72, input.eligibilityRuleCount);
  const purchaseFactor = Math.pow(0.8, input.criteriaRuleCount);
  const eventFactor = input.hasEventRule ? 0.6 : 1;
  const raw = base * profileFactor * purchaseFactor * eventFactor;
  return Math.max(420, Math.round(raw / 10) * 10);
}
