# Coding standards

Extracted from `.cursor/rules` and existing Core/API habits. Do not invent a second aesthetic.

- Onion: controllers validate and delegate; services in `Journeys.Core`; persistence in `Journeys.DAL`; Azure/Auth/Backend/bus/blob in `Journeys.Infra*`. See `docs/platform/architecture.md`.
- Controller and service in/out types are `Journeys.DTO`. `Journeys.Core.Models` stays internal to Core.
- Public async methods are named `...Async` and return `Task` / `Task<T>`.
- Constructor injection; readonly `_camelCase` fields.
- Validation: `APIErrorsException` with `Dictionary<string, string>`; unexpected errors go through `GlobalExceptionMiddleware`.
- Enums serialize as strings (`JsonStringEnumConverter`).
- Prefer `CancellationToken` on async actions when the service stack supports it.
- Tests live in `Journeys.Tests`, existing folders, Arrangeâ€“Actâ€“Assert (`docs/developer/testing.md`).
- Do not delete existing comments without cause.
- Do not take Azure SDK or Backend client dependencies in Core.
