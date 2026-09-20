# Technology Stack

Versions are from the developer scan. Do not treat this list as a license to add packages.

## Languages and runtimes

| Item | Version / note |
|---|---|
| C# / .NET | net8.0 (all onion projects) |
| TypeScript | Journeys.UX only (out of increment) |
| Node / Next.js | Next.js ^16.0.7, React 19 (UX; out of increment) |

## Host and onion frameworks

| Name | Version | Purpose |
|---|---|---|
| ASP.NET Core | net8.0 | `Journeys.API` REST / MCP / consumers |
| Microsoft.Extensions.* | 9.0.10 | DI, options, logging (Core / DAL / Notification) |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.15 | API auth |
| Microsoft.EntityFrameworkCore | 9.0.10 | Referenced by Core; unused import on `RulesEngineState` |
| MassTransit | 8.5.5 | Core + API consumers; do not revive process-event publish |
| Swashbuckle | 6.5.0 | OpenAPI |
| Application Insights | 2.22.0 | API telemetry |
| ModelContextProtocol | 1.1.0 | MCP host |
| Serilog | 4.3.0 | Logging + Azure Analytics sinks (registered only when workspace + auth ids set) |

## Test stack

| Name | Version | Purpose |
|---|---|---|
| xUnit | 2.9.3 | `Journeys.Tests` |
| Microsoft.NET.Test.Sdk | 18.0.0 | Test host |
| coverlet.collector | 6.0.3 | Coverage collector; no runsettings / coverage floor file found |
| Vitest | 3 | Journeys.UX only (out of increment) |

## External assemblies

| Name | Binding | Purpose |
|---|---|---|
| Backend.Core | HintPath `..\..\Binaries\` | External engine/data types — not renamed |
| Backend.Dto | HintPath | DTO interop |
| Backend.Llm.Anthropic | HintPath | LLM adapter used by API/Agent, not this increment |

## UX (out of increment)

Next.js ^16.0.7, React 19, Vitest 3. HTTP-only; must not project-reference Core/DAL/Infra.

## Build

- **Type:** dotnet / MSBuild (SDK-style csproj) + npm for UX.
- **Config:** per-project `*.csproj`; `Journeys.UX/package.json`; no `Directory.Build.props` observed.
- **Solution:** `Journeys.sln`.
- **Local gate:** `scripts/agent-verify.ps1` / `scripts/aidlc-agent-verify-sensor.ps1` plus `docs-impact` / `graph-impact`. No `.github/` workflows in this tree.

## Cross-reference

Package-to-package edges: [dependencies.md](dependencies.md). Quality tooling: [code-quality-assessment.md](code-quality-assessment.md).
