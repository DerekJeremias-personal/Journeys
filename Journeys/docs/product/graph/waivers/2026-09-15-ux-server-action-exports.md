# Waiver: move schema name out of `"use server"` file

**Reason:** Next.js 16 allows only async function exports from `"use server"` modules. `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` moved to `schema-names.ts` so Campaigns/Accounts can compile. No product capability, ontology, or graph edge change.

**Nodes touched by path-map (no meaning change):** `campaigns`.
