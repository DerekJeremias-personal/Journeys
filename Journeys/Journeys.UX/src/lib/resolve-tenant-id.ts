export function resolveTenantId(sessionTenant: string | undefined): string {
  const configured = process.env.JOURNEYS_TENANT_ID?.trim();
  if (configured) return configured;
  return sessionTenant?.trim() ?? "";
}
