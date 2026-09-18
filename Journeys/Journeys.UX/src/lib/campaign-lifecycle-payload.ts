import { kebabSaveStatus } from "./campaign-kebab";
import type { Campaign } from "./campaign-types";

function readField(campaign: Campaign, ...keys: string[]): string | undefined {
  const rec = campaign as Record<string, unknown>;
  for (const key of keys) {
    const value = rec[key];
    if (typeof value === "string" && value.trim()) return value.trim();
  }
  return undefined;
}

export function lifecycleSavePayload(
  campaign: Campaign,
  action: "publish" | "unpublish" | "archive"
): Campaign {
  const status = kebabSaveStatus(action);
  if (action !== "unpublish") {
    return { ...campaign, status };
  }

  return {
    id: readField(campaign, "id", "Id") ?? campaign.id,
    name: readField(campaign, "name", "Name") ?? campaign.name,
    extCampaignId: readField(campaign, "extCampaignId", "ExtCampaignId") ?? campaign.extCampaignId,
    startDate: readField(campaign, "startDate", "StartDate") ?? campaign.startDate,
    endDate: readField(campaign, "endDate", "EndDate") ?? campaign.endDate ?? null,
    tenantId: readField(campaign, "tenantId", "TenantId") ?? campaign.tenantId,
    status
  };
}
