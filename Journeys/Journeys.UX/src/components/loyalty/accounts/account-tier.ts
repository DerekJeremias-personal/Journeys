/**
 * Journey/tier helpers shared by the account detail surfaces.
 *
 * Campaign journey payloads arrive with mixed casing (`children` / `Children`,
 * `id` / `Id`), so every reader here accepts both spellings.
 */

export type Loose = Record<string, unknown>;

export type JourneyRef = {
  rootJourneyNodeId?: string | null;
  journeyNodeIds?: string[] | null;
};

export type CampaignWithJourney = {
  id?: string | null;
  name?: string | null;
  journey?: unknown;
};

export function asLoose(value: unknown): Loose {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Loose) : {};
}

export function normalizeId(value: unknown): string {
  return typeof value === "string" ? value.trim().toLowerCase() : "";
}

export function getNodeId(node: Loose): string | null {
  const id = node["id"] ?? node["Id"];
  return typeof id === "string" && id.length > 0 ? id : null;
}

export function getNodeName(node: Loose): string | null {
  const name = node["name"] ?? node["Name"];
  return typeof name === "string" && name.trim().length > 0 ? name.trim() : null;
}

export function getJourneyRootId(journey: Loose): string {
  return normalizeId(journey["rootNodeId"] ?? journey["RootNodeId"] ?? journey["id"] ?? journey["Id"]);
}

export function getJourneyEnrollmentId(journey: Loose): string {
  return normalizeId(journey["id"] ?? journey["Id"]);
}

export function flattenJourneyNodes(node: Loose): Loose[] {
  const out: Loose[] = [node];
  const children = Array.isArray(node["children"])
    ? (node["children"] as Loose[])
    : Array.isArray(node["Children"])
      ? (node["Children"] as Loose[])
      : [];
  for (const child of children) out.push(...flattenJourneyNodes(asLoose(child)));
  return out;
}

export function journeyMatchesAccountJourney(journey: Loose, accountJourney: JourneyRef): boolean {
  const accountRootId = normalizeId(accountJourney.rootJourneyNodeId);
  if (!accountRootId) return false;
  return accountRootId === getJourneyRootId(journey) || accountRootId === getJourneyEnrollmentId(journey);
}

export function resolveCurrentTierLabel(
  campaigns: CampaignWithJourney[],
  accountJourneys: JourneyRef[]
): string | null {
  for (const accountJourney of accountJourneys) {
    const currentNodeId = accountJourney.journeyNodeIds?.length
      ? accountJourney.journeyNodeIds[accountJourney.journeyNodeIds.length - 1]
      : null;
    if (!currentNodeId) continue;

    const campaign = campaigns.find((candidate) =>
      journeyMatchesAccountJourney(asLoose(candidate.journey), accountJourney)
    );
    if (!campaign) continue;

    const currentNode = flattenJourneyNodes(asLoose(campaign.journey)).find(
      (node) => normalizeId(getNodeId(node)) === normalizeId(currentNodeId)
    );

    return getNodeName(asLoose(currentNode)) ?? currentNodeId;
  }

  return null;
}
