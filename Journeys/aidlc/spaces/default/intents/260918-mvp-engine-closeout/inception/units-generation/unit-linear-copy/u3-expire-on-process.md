# U3 — `u3-expire-on-process`

## U3 — `u3-expire-on-process`

**Description:** On ProcessEvent, bring points current after `TryLockAccount` and before navigation/rules so this event can spend a released hold.

**Boundaries:** `EventService.ProcessCampaignsAsync` order. Calls existing `BringLoyaltyAccountPointsCurrentInternalAsync`. No new lock, sweep, or `/points/expire`.

**Constraints:** Depends on U2 for hop math inside bring-current. Do not implement unlocked pre-`ProcessCampaignsAsync` placement (spec G2). GET/reconcile callers unchanged. Invalid/missing accounts skip bring-current.
