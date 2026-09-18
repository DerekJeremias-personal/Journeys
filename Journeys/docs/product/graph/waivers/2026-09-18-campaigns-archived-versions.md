# Waiver: Campaigns IA archived + versions

**Reason:** Task 7 lifts EXP archived list and version history onto `/loyalty/campaigns/archived` and `/loyalty/campaigns/versions/[extCampaignId]`. Restore uses the existing Campaign restore HTTP endpoint (new Draft, same `ExtCampaignId`; archive row unchanged). No new capability, ontology term, or graph edge.

**Nodes touched by path-map (no meaning change):** `campaigns`.
