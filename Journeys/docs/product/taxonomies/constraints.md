# Taxonomy: constraints

| Id | Rule |
|----|------|
| `tenant-id-required` | Business activity is driven from `TenantId` |
| `no-controller-business-logic` | Controllers validate and delegate; services own logic |
| `backend-dll-external` | `Backend.Core` / `Backend.Dto` / `Backend.Llm.Anthropic` stay named and HintPath-referenced |
| `no-ui-in-sln` | Next.js UI is `Journeys.UX` (HTTP client to `Journeys.API`, not a csproj in `Journeys.sln`) |
