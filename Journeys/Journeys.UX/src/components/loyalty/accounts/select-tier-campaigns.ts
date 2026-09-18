import type { CampaignListItem } from "@/lib/api-types";

/**
 * Tier campaigns are not a distinct campaign kind on the API, so prefer the
 * Live campaigns whose name mentions a tier and fall back to every Live
 * campaign when none do.
 */
export function selectTierCampaigns(campaigns: CampaignListItem[]): CampaignListItem[] {
  const candidates = campaigns.filter(
    (campaign) => Boolean(campaign.id) && (campaign.status ?? "").toLowerCase() === "live"
  );
  const named = candidates.filter((campaign) =>
    (campaign.name ?? "").toLowerCase().includes("tier")
  );
  return named.length > 0 ? named : candidates;
}
