export function resolveTenantId(sessionTenant: string | undefined): string {
  const configured = process.env.JOURNEYS_TENANT_ID?.trim();
  const raw = configured || sessionTenant?.trim() || "";
  // Persistence partitions on lowercase TenantId (DAL upserts). Mixed-case env
  // values would otherwise miss catalog and event rows.
  return raw.toLowerCase();
}
