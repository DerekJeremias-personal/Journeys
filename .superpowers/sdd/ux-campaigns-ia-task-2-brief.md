### Task 2: Core copy + restore

**Files:**
- Create: `Journeys.DTO/Requests/CopyCampaignRequest.cs`
- Create: `Journeys.Core/Services/CampaignCopyFactory.cs`
- Create: `Journeys.Tests/Services/CampaignCopyFactoryTests.cs`
- Modify: `Journeys.Core/Interfaces/Services/ICampaignService.cs`
- Modify: `Journeys.Core/Services/CampaignService.cs`
- Create: `Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs`
- Modify: `Journeys.API/Controllers/CampaignController.cs`

**Interfaces:**
- Consumes: `FetchCampaignAsync`, `UpsertCampaignAsync`, `GetDraftCampaignByExtIdAsync`
- Produces:
  - `CampaignCopyFactory.ForNewProgram(CampaignDto source, string? nameOverride): CampaignDto`
  - `CampaignCopyFactory.ForRestoreFromArchive(CampaignDto archive): CampaignDto`
  - `ICampaignService.CopyCampaignAsync(string tenantId, string campaignId, string status, string? name, CancellationToken cancellationToken = default): Task<CampaignDto>`
  - `ICampaignService.RestoreArchivedCampaignAsync(string tenantId, string campaignId, CancellationToken cancellationToken = default): Task<CampaignDto>`
  - `POST api/Campaign/{tenantId}/{campaignId}/copy?status=`
  - `POST api/Campaign/{tenantId}/{campaignId}/restore?status=archive` (status query required and must be archive)

- [ ] **Step 1: Failing factory tests**

`Journeys.Tests/Services/CampaignCopyFactoryTests.cs`:

```csharp
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignCopyFactoryTests
{
    private static CampaignDto Source() => new()
    {
        Id = "src-id",
        Etag = "etag",
        ExtCampaignId = "summer",
        Name = "Summer",
        Status = "live",
        StartDate = DateTimeOffset.Parse("2026-01-01Z"),
        Events = new List<string> { "evt" },
        DeployedDate = DateTimeOffset.Parse("2026-02-01Z"),
        ArchivedDate = DateTimeOffset.Parse("2026-03-01Z")
    };

    [Fact]
    public void ForNewProgram_new_id_clears_ext_draft_name_suffix()
    {
        var copy = CampaignCopyFactory.ForNewProgram(Source(), null);
        Assert.False(string.IsNullOrWhiteSpace(copy.Id));
        Assert.NotEqual("src-id", copy.Id);
        Assert.True(string.IsNullOrWhiteSpace(copy.ExtCampaignId));
        Assert.Equal("draft", copy.Status);
        Assert.Equal("Summer Copy", copy.Name);
        Assert.Null(copy.Etag);
        Assert.Null(copy.DeployedDate);
        Assert.Null(copy.ArchivedDate);
        Assert.Equal(new List<string> { "evt" }, copy.Events);
    }

    [Fact]
    public void ForNewProgram_uses_name_override()
    {
        var copy = CampaignCopyFactory.ForNewProgram(Source(), "Other");
        Assert.Equal("Other", copy.Name);
    }

    [Fact]
    public void ForRestoreFromArchive_new_id_same_ext_draft()
    {
        var archive = Source();
        archive.Status = "archive";
        var draft = CampaignCopyFactory.ForRestoreFromArchive(archive);
        Assert.NotEqual("src-id", draft.Id);
        Assert.Equal("summer", draft.ExtCampaignId);
        Assert.Equal("draft", draft.Status);
        Assert.Equal("Summer", draft.Name);
        Assert.Null(draft.Etag);
        Assert.Null(draft.DeployedDate);
        Assert.Null(draft.ArchivedDate);
    }
}
```

- [ ] **Step 2: `dotnet test` that class â€” expect FAIL** (type missing)

- [ ] **Step 3: Implement `CampaignCopyFactory`**

Deep-clone via `JsonSerializer` (same assembly `CampaignDto`) then apply field rules. New `Id` = `Guid.NewGuid().ToString()`. Copy must **not** keep `ExtCampaignId`. Restore **must** keep it. Status lowercase `draft`.

- [ ] **Step 4: Factory tests PASS**

- [ ] **Step 5: Service tests**

`CampaignServiceCopyRestoreTests`: recording `ICampaignAdapter` (throw `NotImplementedException` on unused members). `FetchCampaignAsync` returns a `Campaign` with id/status/name/ext. `UpsertCampaignAsync` captures the stored campaign and returns it. `GetDraftCampaignByExtIdAsync` returns null or a draft.

Cases:
1. `CopyCampaignAsync` fetches source, upserts clone with new id, empty ext, draft, name suffix.
2. `RestoreArchivedCampaignAsync` throws `APIErrorsException` when `GetDraftCampaignByExtIdAsync` returns a draft (`errors` key e.g. `extCampaignId`).
3. Restore when no draft: upsert new id, **same** ext, status draft. Adapter must not be asked to upsert status `archive`.

Wire `CampaignService` with `CampaignTestServices.CreateDefinitionValidator()` like `CampaignServiceDeletePatTests`. If definition validation blocks upsert, clone through the factory and have the service call `_campaignAdapter.UpsertCampaignAsync` after `CampaignShellValidator` only **if** that matches existing upsert (prefer calling `UpsertCampaignAsync` so definition rules stay). If the validator requires a journey, put a minimal valid journey on the source fixture using existing test factories.

- [ ] **Step 6: Implement service methods and interface**

```csharp
Task<CampaignDto> CopyCampaignAsync(string tenantId, string campaignId, string status, string? name = null, CancellationToken cancellationToken = default);
Task<CampaignDto> RestoreArchivedCampaignAsync(string tenantId, string campaignId, CancellationToken cancellationToken = default);
```

Copy: fetch source (null â†’ throw/NotFound at controller). `CampaignCopyFactory.ForNewProgram` then `UpsertCampaignAsync`.

Restore: fetch with status `archive`. If null, not found. If `GetDraftCampaignByExtIdAsync` returns a document, throw `new APIErrorsException(new Dictionary<string, string> { ["extCampaignId"] = "A draft already exists for this program." })`. Else factory restore + upsert.

Do **not** call `IAdminAuditService`.

- [ ] **Step 7: Controller endpoints**

Follow `SaveCampaignAsync` style: validate tenant/id/status, call service, `APIErrorsException` â†’ 400 `{ errors }`, unexpected â†’ 500 safe message. `CopyCampaignRequest` body optional.

```csharp
public class CopyCampaignRequest
{
    public string? Name { get; set; }
}
```

Log `TenantId`, `campaignId`, action â€” not full JSON.

- [ ] **Step 8: Verify**

From `C:\Dev\Journeys\Journeys`:

```powershell
.\scripts\agent-verify.ps1 -Files @(
  "Journeys.Core/Services/CampaignCopyFactory.cs",
  "Journeys.Core/Services/CampaignService.cs",
  "Journeys.API/Controllers/CampaignController.cs",
  "Journeys.Tests/Services/CampaignCopyFactoryTests.cs",
  "Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs"
) -RunTests
```

Expected: `agent-verify: OK`. Do not commit unless asked.

---

