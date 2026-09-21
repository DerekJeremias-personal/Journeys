# Build instructions

Working directory is the onion folder: `C:\Dev\Journeys\Journeys` (`Journeys.sln`). Git root is the parent `C:\Dev\Journeys`.

## Dependency installation

1. Install the .NET 8 SDK (`net8.0`).
2. Restore the solution. Backend.* assemblies are HintPath references already in the tree; do not add NuGet packages for them.

```powershell
dotnet restore .\Journeys.sln
```

## Environment setup

- Local API secrets stay in user secrets or `appsettings.Local.json`. Do not commit them.
- Construction verify does not need live Cosmos, blob, Service Bus, or Auth0.
- `Journeys.UX` is out of increment. Do not run `npm install` for this stage.
- Do not log secrets, full event payloads, webhook auth headers, or raw audit JSON while diagnosing builds.

## Build commands

```powershell
dotnet build .\Journeys.sln -c Debug --no-restore
```

If restore is stale, drop `--no-restore`.

## Build verification

- Exit code `0`.
- No new errors in `Journeys.Core`, `Journeys.DTO`, `Journeys.API`, `Journeys.Notification`, or `Journeys.Tests`.
- Gitignored `bin` / `obj` / `.tmp-probe` output is not product source. Do not add those paths to unit source manifests.

Construction verify (docs + graph + build; add `-RunTests` only when `Journeys.Tests` files changed and you intend the script’s own test pass):

```powershell
.\scripts\aidlc-agent-verify-sensor.ps1
```

Do not pass a `Journeys.Tests` path in `-Files` unless you want the script to treat the whole test project as in-blast. Closeout oracles use the filtered `dotnet test` commands in the unit-test instruction files.

## Troubleshooting

- **HintPath / Backend DLL missing:** restore from the existing `Backend.*` layout; do not retarget.
- **PointAccountTypeCache uninitialized in tests:** register the test campaign factory point-account types (same stub used by expire-on-process and notification tests).
- **Full `Journeys.Tests` suite noise:** `UserPointsTests` historical GET/reconcile fixtures and `BatchJobAdapterIntegrationTests` are not this increment’s oracles. Use the filters in `unit-test-instructions.md`.
- **Verify rebuilds `bin`/`pdb`:** expected. Those paths are gitignored.
