export function mapLoyaltyPath(path: string, tenantId: string): string {
  const tenant = tenantId.trim();
  if (!tenant) throw new Error("tenantId is required");
  const trimmed = path.replace(/^\/+/, "");

  if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
    return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
  }
  if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
    return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
  }
  const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
  if (events) {
    return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
  }
  throw new Error(`not-allowlisted: ${trimmed}`);
}
