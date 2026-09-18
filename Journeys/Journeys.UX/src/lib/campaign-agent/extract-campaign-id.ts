const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function findCampaignId(value: unknown, depth = 0): string | null {
  if (depth > 8 || value === null || value === undefined || typeof value !== "object") {
    return null;
  }

  if (!Array.isArray(value)) {
    const object = value as Record<string, unknown>;
    for (const key of [
      "id",
      "Id",
      "campaignId",
      "CampaignId",
      "linkedCampaignId",
      "LinkedCampaignId"
    ] as const) {
      const candidate = object[key];
      if (typeof candidate === "string" && UUID_RE.test(candidate.trim())) {
        return candidate.trim();
      }
    }
  }

  const items = Array.isArray(value) ? value : Object.values(value);
  for (const item of items) {
    const found = findCampaignId(item, depth + 1);
    if (found) return found;
  }
  return null;
}

export function extractCampaignIdFromMcpData(data: string): string | null {
  try {
    return findCampaignId(JSON.parse(data) as unknown);
  } catch {
    return null;
  }
}
