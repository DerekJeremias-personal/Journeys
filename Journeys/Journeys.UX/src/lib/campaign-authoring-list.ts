import type { CampaignListItem } from "./api-types";

export const AUTHORING_CAMPAIGN_STATUSES = ["live", "draft", "pause"] as const;

export function mergeCampaignLists(pages: CampaignListItem[][]): CampaignListItem[] {
  const seen = new Set<string>();
  const out: CampaignListItem[] = [];
  for (const page of pages) {
    for (const row of page) {
      const key = `${row.id ?? ""}:${(row.status ?? "").toLowerCase()}`;
      if (seen.has(key)) continue;
      seen.add(key);
      out.push(row);
    }
  }
  return out;
}
