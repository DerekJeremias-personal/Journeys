export type CampaignKebabVisibility = {
  edit: boolean;
  agent: boolean;
  duplicate: boolean;
  versions: boolean;
  publish: boolean;
  unpublish: boolean;
  archive: boolean;
  restore: boolean;
  delete: boolean;
};

export function normalizeCampaignStatus(raw?: string): string {
  const status = raw?.trim().toLowerCase() ?? "";
  if (status === "active") return "live";
  if (status === "archived") return "archive";
  return status;
}

export function campaignKebabVisibility(
  status: string,
  extCampaignId?: string
): CampaignKebabVisibility {
  const normalized = normalizeCampaignStatus(status);
  const isDraft = normalized === "draft" || normalized === "";
  const isLive = normalized === "live";
  const isArchive = normalized === "archive";
  const isPause = normalized === "pause";

  return {
    edit: true,
    agent: isLive,
    duplicate: true,
    versions: Boolean(extCampaignId),
    publish: isDraft || isPause,
    unpublish: isLive,
    archive: !isArchive && !isDraft,
    restore: isArchive,
    delete: isDraft
  };
}

export function kebabSaveStatus(
  action: "publish" | "unpublish" | "archive"
): "live" | "pause" | "archive" {
  if (action === "publish") return "live";
  if (action === "unpublish") return "pause";
  return "archive";
}
