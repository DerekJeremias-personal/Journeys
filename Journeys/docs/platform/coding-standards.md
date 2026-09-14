# Coding standards

Read this file before adding or changing C# in Journeys.

Layer rules (`docs/platform/architecture.md`, overlays, and path-scoped `.cursor/rules`) stay the source for boundaries. This file covers language, naming, and style. Test placement stays in `.cursor/rules/tests.mdc`.

## Language

- Target net8. Nullable enable. Implicit usings. File-scoped namespaces.
- Public async methods are named `...Async` and return `Task` / `Task<T>`. Put `CancellationToken` last; default to `default` when callers may omit it.
- Constructor injection. Store dependencies in `readonly` `_camelCase` fields.
- Public API in/out types live in `Journeys.Dto`. JSON is camelCase. Document id fields are `id`.
- Do not invent exception hierarchies. Use existing result/error patterns in this repo.
- Do not reformat files you did not change.

## Host and composition

- Composition lives in the Journeys host project. Keep `Program.cs` thin: call extension methods.
- Register request-scoped services as Scoped unless there is a clear Singleton or Transient reason.

## Controllers and UI

- No business rules in API controllers. Controllers map HTTP to Core services.
- The Next.js UI is not in this solution and is not the write authority.

## Tests

See `.cursor/rules/tests.mdc`. Automated tests live in `Journeys.Tests`.
