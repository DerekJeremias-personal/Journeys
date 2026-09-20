# Story map — MVP engine closeout

Every story is assigned. Every unit has at least one story.

## Assignments

| Story | Unit ID | Directory | Notes |
|-------|---------|-----------|-------|
| US1.1 | U1 | `u1-tree-hydrate` | Child/nav hydrate |
| US2.1 | U2 | `u2-earn-date-cascade` | Earn-date dest clock |
| US3.1 | U3 | `u3-expire-on-process` | Lock → bring-current → rules |
| US4.1 | U4 | `u4-notification-webhook` | Webhook, MCP, throw switch |
| US5.1 | U4 | `u4-notification-webhook` | Docs/graph folded into last code unit |

## Cross-cutting

| Concern | Units | How it is handled |
|---------|-------|-------------------|
| Cascade hops during bring-current | U2, U3 | DAG edge U3 → U2; not a second story |
| Award loop honors `IsAwarded` | U4 | AC4.1.9 on the webhook unit; same `RulesService` type as U1 |
| `INotificationService` on state | U4 | Host/`EventService` copy onto state; not a U3-owned unit edge |
| Docs/graph | U4 | US5.1 rides U4; verify scripts on that unit |

## Order within a unit

| Unit | Story order inside the unit |
|------|-----------------------------|
| U1 | US1.1 only |
| U2 | US2.1 only |
| U3 | US3.1 only |
| U4 | US4.1 then US5.1 (docs after the seam is specified) |

## Coverage

| Check | Result |
|-------|--------|
| US1.1–US5.1 assigned | yes |
| U1–U4 have stories | yes |
| Unassigned stories | none |
