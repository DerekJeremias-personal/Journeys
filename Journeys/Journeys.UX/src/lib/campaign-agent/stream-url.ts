export function campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const base = apiBaseUrl.trim().replace(/\/+$/, "");
  if (!base) throw new Error("JOURNEYS_API_BASE_URL is not configured");
  return `${base}/api/v1/${encodeURIComponent(tenant)}/campaign-agent/messages/stream`;
}
