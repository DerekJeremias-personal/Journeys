**Collaborator:** aidlc-developer-agent

## Contribution

Developer spoke, Practices Discovery, intent `mvp-engine-closeout`. Assessed naming, layer boundaries, error handling, file organization, and code-style conventions against the lead draft. Sources: `team-practices.md`, `discovered-rules.md`, `evidence.md`, CodeKB `code-structure.md`, `docs/developer/coding-standards.md`, `docs/platform/coding-standards.md`.

The Code Style and onion bullets are the right baseline. Do not invent a second aesthetic, formatter, or exception hierarchy. Keep increment non-goals out of `discovered-rules.md`. Integrate the additions below into `## Code Style` (and the matching evidence inference) so Construction follows documented C# seams, not org Prettier defaults.

### Layer boundaries (integrate)

- Keep the draft onion: controllers validate and map HTTP; Core owns business rules; DAL is persistence; Azure / Auth / Backend / bus / blob live in `Journeys.Infra*`; `Journeys.Notification` is adapters only (`rest_api`). Controller and service in/out types are `Journeys.DTO`.
- Add the missing developer-standards rule: `Journeys.Core.Models` stays internal to Core. Do not leak Core models across the HTTP/MCP boundary; new closeout contracts (for example `NotificationOutcomePayload`) go under `Journeys.DTO/Models/`.
- Ports live in `Journeys.Core/Interfaces/` (`I*` consumed by Core). Implementations stay in DAL / Infra* / Notification adapters. Do not take Azure SDK or Backend client dependencies in Core.
- Host composition stays in `Journeys.API`: keep `Program.cs` thin and call extension methods. Register request-scoped services as Scoped unless there is a clear Singleton or Transient reason. Controllers stay thin.
- Walking-skeleton / engine writes stay on the Core + DTO path. Do not add a parallel write path in MCP or Campaign Agent handlers. Do not project-reference Core/DAL/Infra from `Journeys.UX` (out of increment; UX is not the write authority).

### Naming and serialization (integrate)

- Assembly and folder name is `Journeys.DTO`. Platform coding-standards writes `Journeys.Dto` for the same package — that is doc casing drift, not a second project. Do not create a `Journeys.Dto` namespace, folder, or csproj.
- HTTP/DTO JSON is camelCase. Document id fields are `id`. Enums serialize as strings (`JsonStringEnumConverter`). Persist wrapper JSON keeps lowercase Backend symbols (`appliedcampaigns`, `outcomestates`, …); do not “fix” those to camelCase.
- Outcome types use `Kind` discriminators (`OutcomeKindDiscriminators`), not `$type`. Nested providers may still use `$type`. Do not introduce `$type` on new outcomes.
- Public async methods are `...Async` and return `Task` / `Task<T>`. Put `CancellationToken` last; default to `default` when callers may omit it. Constructor injection; readonly `_camelCase` fields. File-scoped namespaces, nullable enable, implicit usings, net8.0.
- Two-phase outcomes stay `CalculateOutcomeAsync` then `AwardOutcomeAsync`. Do not rename or reshape that pair as a style cleanup.

### Error handling (integrate)

- Retag the exception bullet from **[Inferred]** to **[Affirmed]**: it is already in `docs/developer/coding-standards.md` and `docs/platform/coding-standards.md`, not a git guess.
- Validation uses existing `APIErrorsException` with `Dictionary<string, string>`. Unexpected errors go through `GlobalExceptionMiddleware`. Do not invent a new exception hierarchy or a Result type that the repo does not already use on these seams.
- Fail closed at the HTTP boundary; do not swallow exceptions in Core. The `NEVER log secrets, full event payloads, webhook auth headers, or raw audit JSON` rule in `discovered-rules.md` is the correct hard constraint for this spoke; leave it there, do not duplicate it as a style ALWAYS.

### File organization (integrate)

- New production types follow CodeKB classification: domain services in `Journeys.Core/Services/`; rules engine under `Journeys.Core/RulesEngine/` (Journey / Engine / Outcomes / Rules / Providers); DTOs in `Journeys.DTO/{Models,Requests,Responses}/`; notification adapters in `Journeys.Notification/Adapters/`.
- New tests live in `Journeys.Tests` and mirror the area folder (RulesEngine/Journey, RulesEngine/Outcomes, Services, …) with Arrange–Act–Assert. Planned closeout tests belong next to those existing folders, not a new test tree.
- Do not add `.editorconfig` or `Directory.Build.props` in this increment unless the interview asks. C# style is the coding-standards docs. Do not reformat files you did not change. Do not delete existing comments without cause. Do not rename historical folder spellings or unused ports (for example `Comparitors`, unused `IRestApiAdapter`) as drive-by cleanup.

### Discovered rules

- Agree: no new ALWAYS/NEVER for naming or layering. Those belong in `team-practices.md` `## Code Style`. The existing ALWAYS TenantId / Core+DTO write path and NEVER log secrets are the only hard constraints this spoke needs.

## Positions

- AGREE: Onion, DTO in/out, persist-lowercase Backend JSON, no second aesthetic, no new formatter, no new exception hierarchy — matches both coding-standards docs and CodeKB `code-structure.md`.
- AGREE: Style conventions stay in `team-practices.md` Code Style; increment non-goals stay out of `discovered-rules.md`; no invented ALWAYS/NEVER.
- AGREE: Tests mirror existing `Journeys.Tests` area folders with Arrange–Act–Assert; UX formatter stack is out of increment.
- OBJECT: `APIErrorsException` / `GlobalExceptionMiddleware` is tagged **[Inferred]** — retag **[Affirmed]**; both coding-standards files already require that pattern.
- OBJECT: Code Style omits `Journeys.Core.Models` stays internal to Core — required layer rule in `docs/developer/coding-standards.md`; closeout DTO payloads must not become Core models.
- OBJECT: Code Style omits enums-as-strings (`JsonStringEnumConverter`) and platform host rules (thin `Program.cs`, Scoped default, `CancellationToken` last with `default`) — Construction will touch composition and async seams.
- OBJECT: Code Style is silent on platform `Journeys.Dto` vs assembly `Journeys.DTO` and on outcome `Kind` (not `$type`) — without those lines, Code Generation can invent a second package or a `$type` discriminator.
