# Logging

## Sinks

`Journeys.API` uses Serilog (`Program.cs`). Azure Analytics is registered only when `Serilog:WriteTo:0:Args:workspaceId` and `authenticationId` are both set. Otherwise the host logs to the console. `JourneysLog` and `JourneysErrorLog` are config names for those table/id settings â€” not a second logging API.

## What to include

When the call is tenant-scoped, use structured properties and include `TenantId` (existing controllers already do this on errors).

## What not to log (interim)

Do not log secrets, API keys, JWT or raw tokens, connection strings, or full event payloads. Do not add new email or account-xref fields to logs. Do not add new startup lines that print workspace or authentication id prefixes. Existing `Program.cs` startup diagnostics stay until a human removes them.
