# Overlays

| Layer | Generic name | This product |
|---|---|---|
| API | `{Product}.API` | `Journeys.API`, `Journeys.Agent` |
| Core | `{Product}.Core` | `Journeys.Core` |
| Dto | `{Product}.Dto` | `Journeys.DTO` |
| Adapters | `{Product}.Adapters` | `Journeys.DAL` + `Journeys.Infra*` + `Journeys.Notification` |
| UI | `{Product}.UX` | `Journeys.UX` (HTTP to API only; not a csproj) |

Dto is required. Adapter path rules remain `dal-adapters.mdc` and `infra-adapters.mdc`; `adapters.mdc` is an alias.
