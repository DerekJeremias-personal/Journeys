# Waiver: screen-owned Backend model ids

**Reason:** UX Accounts queries `LoyaltyAccountDetails` by schema name (the Accounts screen). UX Campaigns uses Campaign `getall`; DAL supplies `CAMPAIGN_MODEL_ID`. Infra Backend adapters no longer coalesce a missing model id to `"unknown"` on entity routes. Catalog `/model/all` still lists by `modelType: loyalty` with no `ModelId`. No new capability or graph edge.

**Nodes touched by path-map (no meaning change):** `campaigns`, `backend-dll-external`.
