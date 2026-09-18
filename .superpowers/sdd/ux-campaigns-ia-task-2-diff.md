# Task 2 review package (working tree; no commit)

## git diff --stat

 .../Journeys.API/Controllers/CampaignController.cs | 130 +++++++++++++++++++++  .../Interfaces/Services/ICampaignService.cs        |  10 ++  Journeys/Journeys.Core/Services/CampaignService.cs |  46 ++++++++  .../Mcp/JourneysMcpToolsCampaignJsonTests.cs       |  14 +++  .../CampaignAssistantContextServiceTests.cs        |  14 +++  5 files changed, 214 insertions(+)

## untracked

Journeys/Journeys.Core/Services/CampaignCopyFactory.cs
Journeys/Journeys.DTO/Requests/CopyCampaignRequest.cs
Journeys/Journeys.Tests/Services/CampaignCopyFactoryTests.cs
Journeys/Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs

## Diff

diff --git a/Journeys/Journeys.API/Controllers/CampaignController.cs b/Journeys/Journeys.API/Controllers/CampaignController.cs
index bb70289..230aabc 100644
--- a/Journeys/Journeys.API/Controllers/CampaignController.cs
+++ b/Journeys/Journeys.API/Controllers/CampaignController.cs
@@ -1,17 +1,18 @@
 using Journeys.Core.Interfaces.Services;
 using Journeys.Core.Models;
 using Journeys.DTO.Exceptions;
 using Journeys.DTO.Models;
 using Journeys.DTO.Requests;
 using Journeys.DTO.Responses;
 using Microsoft.AspNetCore.Mvc;
+using Microsoft.AspNetCore.Mvc.ModelBinding;
 using Microsoft.Extensions.Logging;
 
 namespace Journeys.API.Controllers
 {
 
     [ApiController]
     [Route("api/[controller]")]
     public class CampaignController : ControllerBase
     {
 
@@ -232,20 +233,149 @@ namespace Journeys.API.Controllers
                 return BadRequest(new { errors = ex.Errors });
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Unexpected error saving campaign {CampaignId} (ExtCampaignId: {ExtCampaignId}) for tenant {TenantId}", 
                     req.Id, req.ExtCampaignId, tenantId);
                 return StatusCode(500, new { error = "An unexpected error occurred while saving the campaign." });
             }
         }
 
+        [HttpPost("{tenantId}/{campaignId}/copy")]
+        public async Task<ActionResult<CampaignDto>> CopyCampaignAsync(
+            string tenantId,
+            string campaignId,
+            [FromQuery] string status,
+            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CopyCampaignRequest? request = null,
+            CancellationToken cancellationToken = default)
+        {
+            if (string.IsNullOrWhiteSpace(tenantId))
+                return BadRequest(new { error = "Tenant ID is required.", tenantId });
+
+            if (string.IsNullOrWhiteSpace(campaignId))
+                return BadRequest(new { error = "Campaign ID is required.", campaignId });
+
+            if (string.IsNullOrWhiteSpace(status))
+                return BadRequest(new { error = "Campaign status is required.", status });
+
+            try
+            {
+                _logger.LogInformation(
+                    "Copy campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "copy");
+
+                var copy = await _campaignService.CopyCampaignAsync(
+                    tenantId,
+                    campaignId,
+                    status,
+                    request?.Name,
+                    cancellationToken);
+                if (copy == null)
+                {
+                    return NotFound(new
+                    {
+                        error = $"Campaign with ID '{campaignId}' and status '{status}' not found.",
+                        tenantId,
+                        campaignId,
+                        status
+                    });
+                }
+
+                return Ok(copy);
+            }
+            catch (APIErrorsException ex)
+            {
+                _logger.LogError(
+                    ex,
+                    "Validation error copying campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "copy");
+                return BadRequest(new { errors = ex.Errors });
+            }
+            catch (Exception ex)
+            {
+                _logger.LogError(
+                    ex,
+                    "Unexpected error copying campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "copy");
+                return StatusCode(500, new { error = "An unexpected error occurred while copying the campaign." });
+            }
+        }
+
+        [HttpPost("{tenantId}/{campaignId}/restore")]
+        public async Task<ActionResult<CampaignDto>> RestoreArchivedCampaignAsync(
+            string tenantId,
+            string campaignId,
+            [FromQuery] string status,
+            CancellationToken cancellationToken = default)
+        {
+            if (string.IsNullOrWhiteSpace(tenantId))
+                return BadRequest(new { error = "Tenant ID is required.", tenantId });
+
+            if (string.IsNullOrWhiteSpace(campaignId))
+                return BadRequest(new { error = "Campaign ID is required.", campaignId });
+
+            if (!CampaignStatusStrings.Archive.Equals(status, StringComparison.OrdinalIgnoreCase))
+                return BadRequest(new { error = "Campaign status must be archive.", status });
+
+            try
+            {
+                _logger.LogInformation(
+                    "Restore campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "restore");
+
+                var restored = await _campaignService.RestoreArchivedCampaignAsync(
+                    tenantId,
+                    campaignId,
+                    cancellationToken);
+                if (restored == null)
+                {
+                    return NotFound(new
+                    {
+                        error = $"Archived campaign with ID '{campaignId}' not found.",
+                        tenantId,
+                        campaignId,
+                        status
+                    });
+                }
+
+                return Ok(restored);
+            }
+            catch (APIErrorsException ex)
+            {
+                _logger.LogError(
+                    ex,
+                    "Validation error restoring campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "restore");
+                return BadRequest(new { errors = ex.Errors });
+            }
+            catch (Exception ex)
+            {
+                _logger.LogError(
+                    ex,
+                    "Unexpected error restoring campaign {CampaignId} for tenant {TenantId}; action {Action}",
+                    campaignId,
+                    tenantId,
+                    "restore");
+                return StatusCode(500, new { error = "An unexpected error occurred while restoring the campaign." });
+            }
+        }
+
         [HttpDelete("{tenantId}")]
         public async Task<IActionResult> RemoveCampaignAsync(string tenantId, [FromBody] RemoveCampaignRequest req)
         {
             if (string.IsNullOrEmpty(tenantId))
             {
                 return BadRequest(new { error = "Tenant ID is required.", tenantId });
             }
 
             if (req == null || req.Campaign == null)
             {
diff --git a/Journeys/Journeys.Core/Interfaces/Services/ICampaignService.cs b/Journeys/Journeys.Core/Interfaces/Services/ICampaignService.cs
index dbbff99..fdb0bc4 100644
--- a/Journeys/Journeys.Core/Interfaces/Services/ICampaignService.cs
+++ b/Journeys/Journeys.Core/Interfaces/Services/ICampaignService.cs
@@ -11,20 +11,30 @@ namespace Journeys.Core.Interfaces.Services
 {
     public interface ICampaignService
     {
         Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status);
         Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req);
         Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string status = null);
         Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null);
         Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null);
 
         Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign);
+        Task<CampaignDto> CopyCampaignAsync(
+            string tenantId,
+            string campaignId,
+            string status,
+            string? name = null,
+            CancellationToken cancellationToken = default);
+        Task<CampaignDto> RestoreArchivedCampaignAsync(
+            string tenantId,
+            string campaignId,
+            CancellationToken cancellationToken = default);
         Task<CampaignValidationResultDto> ValidateCampaignAsync(
             string tenantId,
             CampaignDto campaign,
             CancellationToken cancellationToken = default);
         Task DeleteCampaignAsync(string tenantId, string campaignId, string status);
         Task DeleteCampaignAsync(string tenantId, CampaignDto campaign);
 
         Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id);
         Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null);
         Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat);
diff --git a/Journeys/Journeys.Core/Services/CampaignService.cs b/Journeys/Journeys.Core/Services/CampaignService.cs
index afb2bf4..be08539 100644
--- a/Journeys/Journeys.Core/Services/CampaignService.cs
+++ b/Journeys/Journeys.Core/Services/CampaignService.cs
@@ -172,20 +172,66 @@ namespace Journeys.Core.Services
                 throw;
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex);
                 throw;
             }
             return campaign;
         }
 
+        public async Task<CampaignDto> CopyCampaignAsync(
+            string tenantId,
+            string campaignId,
+            string status,
+            string? name = null,
+            CancellationToken cancellationToken = default)
+        {
+            cancellationToken.ThrowIfCancellationRequested();
+
+            var source = await FetchCampaignAsync(tenantId, campaignId, status).ConfigureAwait(false);
+            if (source == null)
+                return null;
+
+            var copy = CampaignCopyFactory.ForNewProgram(source, name);
+            return await UpsertCampaignAsync(tenantId, copy).ConfigureAwait(false);
+        }
+
+        public async Task<CampaignDto> RestoreArchivedCampaignAsync(
+            string tenantId,
+            string campaignId,
+            CancellationToken cancellationToken = default)
+        {
+            cancellationToken.ThrowIfCancellationRequested();
+
+            var archive = await FetchCampaignAsync(
+                    tenantId,
+                    campaignId,
+                    CampaignStatusStrings.Archive.ToLowerInvariant())
+                .ConfigureAwait(false);
+            if (archive == null)
+                return null;
+
+            var draft = await GetDraftCampaignByExtIdAsync(tenantId, archive.ExtCampaignId)
+                .ConfigureAwait(false);
+            if (draft != null)
+            {
+                throw new APIErrorsException(new Dictionary<string, string>
+                {
+                    ["extCampaignId"] = "A draft already exists for this program."
+                });
+            }
+
+            var restored = CampaignCopyFactory.ForRestoreFromArchive(archive);
+            return await UpsertCampaignAsync(tenantId, restored).ConfigureAwait(false);
+        }
+
         public async Task DeleteCampaignAsync(string tenantId, string campaignId, string status)
         {
             CampaignDto campaign = null;
             try
             {
                 await _campaignAdapter.DeleteCampaignAsync(tenantId, campaignId, status);
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex);
diff --git a/Journeys/Journeys.Tests/Mcp/JourneysMcpToolsCampaignJsonTests.cs b/Journeys/Journeys.Tests/Mcp/JourneysMcpToolsCampaignJsonTests.cs
index 60cdb1c..3ff1e7d 100644
--- a/Journeys/Journeys.Tests/Mcp/JourneysMcpToolsCampaignJsonTests.cs
+++ b/Journeys/Journeys.Tests/Mcp/JourneysMcpToolsCampaignJsonTests.cs
@@ -94,20 +94,34 @@ public class JourneysMcpToolsCampaignJsonTests
     {
         public Task<CampaignValidationResultDto> ValidateCampaignAsync(
             string tenantId,
             CampaignDto campaign,
             CancellationToken cancellationToken = default) =>
             Task.FromResult(new CampaignValidationResultDto { IsValid = true });
 
         public Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign) =>
             Task.FromResult(campaign);
 
+        public Task<CampaignDto> CopyCampaignAsync(
+            string tenantId,
+            string campaignId,
+            string status,
+            string? name = null,
+            CancellationToken cancellationToken = default) =>
+            throw new NotImplementedException();
+
+        public Task<CampaignDto> RestoreArchivedCampaignAsync(
+            string tenantId,
+            string campaignId,
+            CancellationToken cancellationToken = default) =>
+            throw new NotImplementedException();
+
         public Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status) =>
             throw new NotImplementedException();
 
         public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) =>
             throw new NotImplementedException();
 
         public Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string? status = null) =>
             throw new NotImplementedException();
 
         public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) =>
diff --git a/Journeys/Journeys.Tests/Services/CampaignAssistantContextServiceTests.cs b/Journeys/Journeys.Tests/Services/CampaignAssistantContextServiceTests.cs
index b767b96..63164c3 100644
--- a/Journeys/Journeys.Tests/Services/CampaignAssistantContextServiceTests.cs
+++ b/Journeys/Journeys.Tests/Services/CampaignAssistantContextServiceTests.cs
@@ -261,20 +261,34 @@ public class CampaignAssistantContextServiceTests
 
         public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) =>
             throw new NotImplementedException();
 
         public Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null!) =>
             throw new NotImplementedException();
 
         public Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign) =>
             throw new NotImplementedException();
 
+        public Task<CampaignDto> CopyCampaignAsync(
+            string tenantId,
+            string campaignId,
+            string status,
+            string? name = null,
+            CancellationToken cancellationToken = default) =>
+            throw new NotImplementedException();
+
+        public Task<CampaignDto> RestoreArchivedCampaignAsync(
+            string tenantId,
+            string campaignId,
+            CancellationToken cancellationToken = default) =>
+            throw new NotImplementedException();
+
         public Task<CampaignValidationResultDto> ValidateCampaignAsync(
             string tenantId,
             CampaignDto campaign,
             CancellationToken cancellationToken = default) =>
             throw new NotImplementedException();
 
         public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) =>
             throw new NotImplementedException();
 
         public Task DeleteCampaignAsync(string tenantId, CampaignDto campaign) =>

### NEW FILE Journeys/Journeys.Core/Services/CampaignCopyFactory.cs

diff --git a/Journeys/Journeys.Core/Services/CampaignCopyFactory.cs b/Journeys/Journeys.Core/Services/CampaignCopyFactory.cs
new file mode 100644
index 0000000..e1c81a4
--- /dev/null
+++ b/Journeys/Journeys.Core/Services/CampaignCopyFactory.cs
@@ -0,0 +1,45 @@
+using System;
+using System.Text.Json;
+using Journeys.Core.Models;
+using Journeys.DTO.Models;
+
+namespace Journeys.Core.Services
+{
+    public static class CampaignCopyFactory
+    {
+        public static CampaignDto ForNewProgram(CampaignDto source, string? nameOverride)
+        {
+            var copy = Clone(source);
+            copy.Id = Guid.NewGuid().ToString();
+            copy.Etag = null;
+            copy.ExtCampaignId = null;
+            copy.Status = CampaignStatusStrings.Draft.ToLowerInvariant();
+            copy.Name = nameOverride ?? $"{source.Name} Copy";
+            copy.DeployedDate = null;
+            copy.ArchivedDate = null;
+            copy.AssistantDigest = null;
+            return copy;
+        }
+
+        public static CampaignDto ForRestoreFromArchive(CampaignDto archive)
+        {
+            var draft = Clone(archive);
+            draft.Id = Guid.NewGuid().ToString();
+            draft.Etag = null;
+            draft.Status = CampaignStatusStrings.Draft.ToLowerInvariant();
+            draft.DeployedDate = null;
+            draft.ArchivedDate = null;
+            draft.AssistantDigest = null;
+            return draft;
+        }
+
+        private static CampaignDto Clone(CampaignDto source)
+        {
+            ArgumentNullException.ThrowIfNull(source);
+
+            var json = JsonSerializer.Serialize(source);
+            return JsonSerializer.Deserialize<CampaignDto>(json)
+                ?? throw new InvalidOperationException("Campaign copy could not be created.");
+        }
+    }
+}

### NEW FILE Journeys/Journeys.DTO/Requests/CopyCampaignRequest.cs

diff --git a/Journeys/Journeys.DTO/Requests/CopyCampaignRequest.cs b/Journeys/Journeys.DTO/Requests/CopyCampaignRequest.cs
new file mode 100644
index 0000000..e63075d
--- /dev/null
+++ b/Journeys/Journeys.DTO/Requests/CopyCampaignRequest.cs
@@ -0,0 +1,7 @@
+namespace Journeys.DTO.Requests
+{
+    public class CopyCampaignRequest
+    {
+        public string? Name { get; set; }
+    }
+}

### NEW FILE Journeys/Journeys.Tests/Services/CampaignCopyFactoryTests.cs

diff --git a/Journeys/Journeys.Tests/Services/CampaignCopyFactoryTests.cs b/Journeys/Journeys.Tests/Services/CampaignCopyFactoryTests.cs
new file mode 100644
index 0000000..43b3967
--- /dev/null
+++ b/Journeys/Journeys.Tests/Services/CampaignCopyFactoryTests.cs
@@ -0,0 +1,58 @@
+using Journeys.Core.Services;
+using Journeys.DTO.Models;
+using Xunit;
+
+namespace Journeys.Tests.Services;
+
+public class CampaignCopyFactoryTests
+{
+    private static CampaignDto Source() => new()
+    {
+        Id = "src-id",
+        Etag = "etag",
+        ExtCampaignId = "summer",
+        Name = "Summer",
+        Status = "live",
+        StartDate = DateTimeOffset.Parse("2026-01-01Z"),
+        Events = new List<string> { "evt" },
+        DeployedDate = DateTimeOffset.Parse("2026-02-01Z"),
+        ArchivedDate = DateTimeOffset.Parse("2026-03-01Z")
+    };
+
+    [Fact]
+    public void ForNewProgram_new_id_clears_ext_draft_name_suffix()
+    {
+        var copy = CampaignCopyFactory.ForNewProgram(Source(), null);
+        Assert.False(string.IsNullOrWhiteSpace(copy.Id));
+        Assert.NotEqual("src-id", copy.Id);
+        Assert.True(string.IsNullOrWhiteSpace(copy.ExtCampaignId));
+        Assert.Equal("draft", copy.Status);
+        Assert.Equal("Summer Copy", copy.Name);
+        Assert.Null(copy.Etag);
+        Assert.Null(copy.DeployedDate);
+        Assert.Null(copy.ArchivedDate);
+        Assert.Equal(new List<string> { "evt" }, copy.Events);
+    }
+
+    [Fact]
+    public void ForNewProgram_uses_name_override()
+    {
+        var copy = CampaignCopyFactory.ForNewProgram(Source(), "Other");
+        Assert.Equal("Other", copy.Name);
+    }
+
+    [Fact]
+    public void ForRestoreFromArchive_new_id_same_ext_draft()
+    {
+        var archive = Source();
+        archive.Status = "archive";
+        var draft = CampaignCopyFactory.ForRestoreFromArchive(archive);
+        Assert.NotEqual("src-id", draft.Id);
+        Assert.Equal("summer", draft.ExtCampaignId);
+        Assert.Equal("draft", draft.Status);
+        Assert.Equal("Summer", draft.Name);
+        Assert.Null(draft.Etag);
+        Assert.Null(draft.DeployedDate);
+        Assert.Null(draft.ArchivedDate);
+    }
+}

### NEW FILE Journeys/Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs

diff --git a/Journeys/Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs b/Journeys/Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs
new file mode 100644
index 0000000..e46d75e
--- /dev/null
+++ b/Journeys/Journeys.Tests/Services/CampaignServiceCopyRestoreTests.cs
@@ -0,0 +1,157 @@
+using Journeys.Core.Interfaces.DataStorage;
+using Journeys.Core.Interfaces.Services;
+using Journeys.Core.Models;
+using Journeys.Core.Services;
+using Journeys.DTO.Exceptions;
+using Journeys.DTO.Requests;
+using Microsoft.Extensions.Logging;
+
+namespace Journeys.Tests.Services;
+
+public class CampaignServiceCopyRestoreTests
+{
+    private static readonly ILogger<CampaignService> Logger = new NopLogger<CampaignService>();
+
+    [Fact]
+    public async Task CopyCampaignAsync_fetches_source_and_upserts_new_draft_program()
+    {
+        var adapter = new RecordingCampaignAdapter
+        {
+            Source = Campaign("source-id", "live", "Summer", "summer")
+        };
+        var sut = CreateService(adapter);
+
+        var result = await sut.CopyCampaignAsync("tenant-1", "source-id", "live");
+
+        Assert.Equal(("tenant-1", "source-id", "live"), adapter.FetchCall);
+        Assert.NotNull(adapter.Upserted);
+        Assert.NotEqual("source-id", adapter.Upserted.Id);
+        Assert.Equal("draft", adapter.Upserted.Status);
+        Assert.Equal("summer copy", adapter.Upserted.Name);
+        Assert.Equal("summer copy", adapter.Upserted.ExtCampaignId);
+        Assert.Equal(adapter.Upserted.Id, result.Id);
+    }
+
+    [Fact]
+    public async Task RestoreArchivedCampaignAsync_throws_when_draft_already_exists()
+    {
+        var adapter = new RecordingCampaignAdapter
+        {
+            Source = Campaign("archive-id", "archive", "Summer", "summer"),
+            ExistingDraft = Campaign("draft-id", "draft", "Summer", "summer")
+        };
+        var sut = CreateService(adapter);
+
+        var exception = await Assert.ThrowsAsync<APIErrorsException>(
+            () => sut.RestoreArchivedCampaignAsync("tenant-1", "archive-id"));
+
+        Assert.Equal("A draft already exists for this program.", exception.Errors["extCampaignId"]);
+        Assert.Null(adapter.Upserted);
+    }
+
+    [Fact]
+    public async Task RestoreArchivedCampaignAsync_upserts_new_draft_with_same_external_id()
+    {
+        var adapter = new RecordingCampaignAdapter
+        {
+            Source = Campaign("archive-id", "archive", "Summer", "summer")
+        };
+        var sut = CreateService(adapter);
+
+        var result = await sut.RestoreArchivedCampaignAsync("tenant-1", "archive-id");
+
+        Assert.Equal(("tenant-1", "archive-id", "archive"), adapter.FetchCall);
+        Assert.Equal(("tenant-1", "summer"), adapter.DraftLookupCall);
+        Assert.NotNull(adapter.Upserted);
+        Assert.NotEqual("archive-id", adapter.Upserted.Id);
+        Assert.Equal("summer", adapter.Upserted.ExtCampaignId);
+        Assert.Equal("draft", adapter.Upserted.Status);
+        Assert.NotEqual("archive", adapter.Upserted.Status);
+        Assert.Equal(adapter.Upserted.Id, result.Id);
+    }
+
+    private static CampaignService CreateService(RecordingCampaignAdapter adapter) =>
+        new(
+            adapter,
+            new ThrowingPointAccountTypeAdapter(),
+            new PermissivePointAccountTypeCache(),
+            Logger,
+            CampaignTestServices.CreateDefinitionValidator(),
+            CampaignTestServices.CreateValidationOrchestrator());
+
+    private static Campaign Campaign(string id, string status, string name, string extCampaignId) =>
+        new(
+            extCampaignId,
+            status,
+            name,
+            new List<string> { "evt" },
+            DateTimeOffset.Parse("2026-01-01Z"),
+            null,
+            new List<Segment>(),
+            null,
+            "tenant-1",
+            id);
+
+    private sealed class RecordingCampaignAdapter : ICampaignAdapter
+    {
+        public Campaign? Source { get; init; }
+        public Campaign? ExistingDraft { get; init; }
+        public Campaign? Upserted { get; private set; }
+        public (string TenantId, string CampaignId, string Status)? FetchCall { get; private set; }
+        public (string TenantId, string ExtCampaignId)? DraftLookupCall { get; private set; }
+
+        public Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status)
+        {
+            FetchCall = (tenantId, campaignId, status);
+            return Task.FromResult(Source!);
+        }
+
+        public Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId)
+        {
+            DraftLookupCall = (tenantId, extCampaignId);
+            return Task.FromResult(ExistingDraft!);
+        }
+
+        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign)
+        {
+            Upserted = campaign;
+            return Task.FromResult(campaign);
+        }
+
+        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
+        public Task DeleteCampaignAsync(string tenantId, Campaign Campaign) => throw new NotImplementedException();
+        public Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) => throw new NotImplementedException();
+        public Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status) => throw new NotImplementedException();
+        public Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
+        public Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
+        public Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
+        public Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
+        public Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
+    }
+
+    private sealed class ThrowingPointAccountTypeAdapter : IPointAccountTypeAdapter
+    {
+        public Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId) => throw new NotImplementedException();
+        public Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
+        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
+        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
+        public Task DeletePointAccountTypeAsync(string tenantId, string id) => throw new NotImplementedException();
+    }
+
+    private sealed class PermissivePointAccountTypeCache : IPointAccountTypeCache
+    {
+        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => true;
+        public Task<bool> EnsurePATsLoaded(string tenantId) => Task.FromResult(true);
+        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => Task.FromResult(new List<PointAccountType>());
+        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
+        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => Task.CompletedTask;
+        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => Task.CompletedTask;
+    }
+
+    private sealed class NopLogger<T> : ILogger<T>
+    {
+        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
+        public bool IsEnabled(LogLevel logLevel) => false;
+        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
+    }
+}
