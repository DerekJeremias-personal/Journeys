# Waiver: local UX tenant from JOURNEYS_TENANT_ID

**Reason:** Journeys.UX now always sends `JOURNEYS_TENANT_ID` (`TestTenant1`) on API calls, even if an older NextAuth cookie still holds a previous tenant. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `campaigns`.
