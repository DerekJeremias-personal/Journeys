# Task 2 Report: Core Copy + Restore

## Status

DONE_WITH_CONCERNS

Task 2 is implemented in place. No commit, push, merge, or PR action was performed.

## Implementation

- Added `CopyCampaignRequest` with optional `Name`.
- Added `CampaignCopyFactory` using `JsonSerializer` deep cloning.
  - New-program copy gets a new `Id`, clears `ExtCampaignId`, ETag, deployment/archive dates, and assistant digest, becomes Draft, and uses an override or `{Name} Copy`.
  - Archive restore gets a new `Id`, preserves `ExtCampaignId` and name, clears ETag and deployment/archive dates, and becomes Draft.
- Added `ICampaignService.CopyCampaignAsync` and `RestoreArchivedCampaignAsync`.
- Copy fetches from the requested source partition and persists through the existing validated `UpsertCampaignAsync` path.
- Restore fetches only from `archive`, rejects an existing Draft for the external campaign id, and persists a new Draft through `UpsertCampaignAsync`; it never upserts Archive.
- Added `POST api/Campaign/{tenantId}/{campaignId}/copy?status=...`.
- Added `POST api/Campaign/{tenantId}/{campaignId}/restore?status=archive`.
- Endpoints validate required route/query values, return 404 for absent sources, map `APIErrorsException` to `{ errors }`, return safe 500 messages, and log only structured tenant/campaign/action context.
- Updated two existing test-only `ICampaignService` stubs required by the expanded interface.
- No `IAdminAuditService` was added.

## TDD Evidence

### Factory RED

Command:

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignCopyFactoryTests -p:BaseOutputPath=C:\Dev\Journeys\Temp\task2-red\
```

Expected failure observed:

```text
CampaignCopyFactoryTests.cs(...): error CS0103: The name 'CampaignCopyFactory' does not exist in the current context
```

The first attempt without isolated output was obstructed by the running `Journeys.API` process locking Debug assemblies; the isolated-output rerun produced the intended RED failure.

### Factory GREEN

The factory test class passed after implementation.

### Service RED

Command:

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter FullyQualifiedName~CampaignServiceCopyRestoreTests -p:BaseOutputPath=C:\Dev\Journeys\Temp\task2-service-red\
```

Expected failures observed:

```text
CampaignService does not contain a definition for 'CopyCampaignAsync'
CampaignService does not contain a definition for 'RestoreArchivedCampaignAsync'
```

### Service GREEN

All three service cases passed after implementation. One intermediate assertion exposed that restore fetched `"Archive"` rather than required lowercase `"archive"`; the implementation was corrected and rerun green.

## Final Tests and Build

Targeted Task 2 tests:

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignCopyFactoryTests|FullyQualifiedName~CampaignServiceCopyRestoreTests" -p:BaseOutputPath=C:\Dev\Journeys\Temp\task2-final-tests\
```

Result:

```text
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

The API project built successfully with isolated output. The full solution Debug build was attempted and failed only because the already-running `Journeys.API` process (PID 13612), plus Visual Studio for some files, locked assemblies under `Journeys.API\bin\Debug\net8.0`.

The full solution Release build succeeded:

```text
1677 Warning(s)
0 Error(s)
Time Elapsed 00:00:10.54
```

Warnings are existing repository-wide warnings; no IDE linter errors were reported for the edited files. `git diff --check` passed.

## Required Verify Output

The brief's exact `agent-verify.ps1` command was run. It exited 1 before build/tests with:

```text
docs-impact: FAIL - required docs not in the change set:
  Journeys.Core/Services/CampaignCopyFactory.cs -> docs/platform/architecture.md
  Journeys.Core/Services/CampaignCopyFactory.cs -> docs/product/ontology/campaign.md
  Journeys.Core/Services/CampaignCopyFactory.cs -> docs/product/ontology/draft-live.md
  Journeys.Core/Services/CampaignService.cs -> docs/platform/architecture.md
  Journeys.Core/Services/CampaignService.cs -> docs/product/ontology/campaign.md
  Journeys.Core/Services/CampaignService.cs -> docs/product/ontology/draft-live.md
  Journeys.API/Controllers/CampaignController.cs -> docs/platform/architecture.md
  Journeys.API/Controllers/CampaignController.cs -> docs/platform/security.md
  Journeys.API/Controllers/CampaignController.cs -> docs/product/ontology/tenant.md
  Journeys.Tests/Services/CampaignCopyFactoryTests.cs -> docs/developer/testing.md
  Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs -> docs/developer/testing.md
```

Per the task sequencing, docs/graph synchronization is reserved for Task 8, so those files were not modified here.

## Files

Created:

- `Journeys.DTO/Requests/CopyCampaignRequest.cs`
- `Journeys.Core/Services/CampaignCopyFactory.cs`
- `Journeys.Tests/Services/CampaignCopyFactoryTests.cs`
- `Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs`

Modified:

- `Journeys.Core/Interfaces/Services/ICampaignService.cs`
- `Journeys.Core/Services/CampaignService.cs`
- `Journeys.API/Controllers/CampaignController.cs`
- `Journeys.Tests/Mcp/JourneysMcpToolsCampaignJsonTests.cs`
- `Journeys.Tests/Services/CampaignAssistantContextServiceTests.cs`

## Self-review and Concerns

- Copy and restore use the established validated upsert path, preserving existing campaign definition validation and normalization.
- Restore checks for an existing Draft before creating a new document and does not alter the Archive document.
- Copy intentionally clears `ExtCampaignId`; existing upsert normalization creates the new program identity from the copied/overridden name.
- The required verify command is not green because Task 8 documentation files are intentionally absent.
- The ordinary Debug solution build cannot complete while the user's running API/Visual Studio hold output DLL locks; Release and isolated builds are green.
- No commits were created.

## Fix pass

- Added controller-level copy/restore coverage without starting the web host.
- Copy now validates nonblank lifecycle status through `CampaignShellValidator.ValidateStatus`; invalid values map `APIErrorsException` to `400 { errors }`.
- Restore validates lifecycle status first, then separately requires `archive` (case-insensitive).
- Converted the two new production files to nullable-enabled, file-scoped namespaces.

Controller regression RED:

```text
Failed Journeys.Tests.Controllers.CampaignControllerCopyRestoreTests.CopyCampaignAsync_invalid_status_returns_bad_request_with_errors
Expected: BadRequestObjectResult
Actual:   OkObjectResult
Failed! - Failed: 1, Passed: 7, Skipped: 0, Total: 8
```

Command:

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignCopyFactoryTests|FullyQualifiedName~CampaignServiceCopyRestoreTests|FullyQualifiedName~CampaignControllerCopy" -p:BaseOutputPath=C:\Dev\Journeys\Temp\task2-fix\
```

Output:

```text
Journeys.Tests -> C:\Dev\Journeys\Journeys\Journeys.Tests\bin\Debug\net8.0\Journeys.Tests.dll
Test run for C:\Dev\Journeys\Journeys\Journeys.Tests\bin\Debug\net8.0\Journeys.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 104 ms
```

The build emitted existing repository warnings; there were no test failures.

## Fix pass 2 — unique copy identity

- New-program copies now receive independent lowercase GUID `ExtCampaignId` values instead of relying on display-name normalization.
- Factory and service regressions verify repeated copies of one source have distinct external identities, neither equal to the source identity.
- Archive restore coverage still verifies that restore preserves the archive `ExtCampaignId`.

Command:

```powershell
dotnet test .\Journeys.Tests\Journeys.Tests.csproj --filter "FullyQualifiedName~CampaignCopyFactoryTests|FullyQualifiedName~CampaignServiceCopyRestoreTests|FullyQualifiedName~CampaignControllerCopy" -p:BaseOutputPath=C:\Dev\Journeys\Temp\task2-fix2\
```

Output:

```text
Journeys.Tests -> C:\Dev\Journeys\Journeys\Journeys.Tests\bin\Debug\net8.0\Journeys.Tests.dll
Test run for C:\Dev\Journeys\Journeys\Journeys.Tests\bin\Debug\net8.0\Journeys.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 91 ms
```

The build emitted existing repository warnings; there were no test failures or IDE linter errors in the changed C# files.
