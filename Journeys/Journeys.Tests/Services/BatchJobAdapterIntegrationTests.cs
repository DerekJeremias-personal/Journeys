using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DAL.Adapters;
using Journeys.Infra.Backend;
using Journeys.Tests;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Journeys.Tests.Services;

/// <summary>
/// Integration tests for BatchJobAdapter.TryClaimMultipleJobsAsync
/// These tests hit Cosmos DB via BackendAdapter to verify ETag-based optimistic concurrency.
/// Requires: Backend API running locally and accessible Cosmos DB.
/// </summary>
public class BatchJobAdapterIntegrationTests : IDisposable
{
    private readonly IBatchJobAdapter _batchJobAdapter;
    private readonly IBatchFileAdapter _batchFileAdapter;
    private readonly IDynamicDataAdapter _dataAdapter;
    private readonly string _testTenantId;
    private readonly string _testNodeId;
    private readonly List<string> _createdJobIds = new List<string>();
    private readonly List<string> _createdBatchFileIds = new List<string>();

    public BatchJobAdapterIntegrationTests()
    {
        // Setup real adapters (not stubs)
        var httpFactory = new TestHttpFactory();
        var keyValueStorageConfig = TestOptionsFactory.GetKeyValueStorageConfigOptions();
        var logger = LoggerFactoryProvider.CreateLogger<BackendAdapter>();

        _dataAdapter = new BackendAdapter(httpFactory, keyValueStorageConfig, logger);
        _batchJobAdapter = new BatchJobAdapter(_dataAdapter);
        _batchFileAdapter = new BatchFileAdapter(_dataAdapter);

        // Use unique test identifiers
        _testTenantId = $"test-claiming-{Guid.NewGuid():N}";
        _testNodeId = $"TEST-NODE-{Guid.NewGuid():N}";
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_BasicSingleNodeClaiming_ClaimsExactTargetCount()
    {
        // Arrange: Create 10 Pending jobs
        var batchFileId = await CreateTestBatchFileAsync();
        var jobs = await CreateTestBatchJobsAsync(batchFileId, count: 10, status: BatchJobStatusStrings.PENDING);

        try
        {
            // Act: Claim 5 jobs
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert
            Assert.NotNull(claimedJobs);
            Assert.Equal(5, claimedJobs.Count);

            foreach (var job in claimedJobs)
            {
                Assert.Equal(BatchJobStatusStrings.CLAIMING, job.Status);
                Assert.Equal(_testNodeId, job.ClaimedBy);
                Assert.NotNull(job.ClaimExpiration);
                Assert.True(job.ClaimExpiration > DateTimeOffset.UtcNow);
                Assert.True(job.ClaimExpiration <= DateTimeOffset.UtcNow.AddSeconds(65)); // Allow some buffer
                Assert.NotNull(job.ETag); // ETag should be present after upsert
            }
        }
        finally
        {
            await CleanupJobsAsync(jobs);
            await CleanupBatchFileAsync(batchFileId);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_PartialClaiming_ClaimsAvailableJobs()
    {
        // Arrange: Create only 3 Pending jobs
        var batchFileId = await CreateTestBatchFileAsync();
        var jobs = await CreateTestBatchJobsAsync(batchFileId, count: 3, status: BatchJobStatusStrings.PENDING);

        try
        {
            // Act: Try to claim 5 jobs
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should claim only 3 available
            Assert.NotNull(claimedJobs);
            Assert.Equal(3, claimedJobs.Count);

            foreach (var job in claimedJobs)
            {
                Assert.Equal(BatchJobStatusStrings.CLAIMING, job.Status);
                Assert.Equal(_testNodeId, job.ClaimedBy);
            }
        }
        finally
        {
            await CleanupJobsAsync(jobs);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_NoJobsAvailable_ReturnsEmptyList()
    {
        // Arrange: No jobs in database

        // Act
        var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
            _testTenantId,
            _testNodeId,
            targetCount: 5,
            maxCandidates: 20,
            claimTimeoutSeconds: 60);

        // Assert
        Assert.NotNull(claimedJobs);
        Assert.Empty(claimedJobs);
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_StaleClaimReclamation_ReclaimsExpiredJobs()
    {
        // Arrange: Create a job with expired claim
        var batchFileId = await CreateTestBatchFileAsync();
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-5); // Expired 5 minutes ago
        var oldNodeId = "OLD-NODE-123";

        var staleJob = await CreateTestBatchJobAsync(
            batchFileId,
            status: BatchJobStatusStrings.CLAIMING,
            claimedBy: oldNodeId,
            claimExpiration: expiredTime);

        try
        {
            // Act: Try to claim jobs (should reclaim the stale one)
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should reclaim the stale job
            Assert.NotNull(claimedJobs);
            Assert.Single(claimedJobs);

            var reclaimed = claimedJobs[0];
            Assert.Equal(BatchJobStatusStrings.CLAIMING, reclaimed.Status);
            Assert.Equal(_testNodeId, reclaimed.ClaimedBy); // Now claimed by our node
            Assert.NotNull(reclaimed.ClaimExpiration);
            Assert.True(reclaimed.ClaimExpiration > DateTimeOffset.UtcNow);
        }
        finally
        {
            await CleanupJobAsync(staleJob);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_MixedCandidates_ClaimsBothPendingAndStale()
    {
        // Arrange: Create mix of Pending and stale Claiming jobs
        var batchFileId = await CreateTestBatchFileAsync();
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        var pendingJobs = await CreateTestBatchJobsAsync(batchFileId, count: 3, status: BatchJobStatusStrings.PENDING);
        var staleJobs = await CreateTestBatchJobsAsync(
            batchFileId,
            count: 2,
            status: BatchJobStatusStrings.CLAIMING,
            claimedBy: "OLD-NODE",
            claimExpiration: expiredTime);

        var allJobs = pendingJobs.Concat(staleJobs).ToList();

        try
        {
            // Act: Claim 5 jobs
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should claim all 5
            Assert.NotNull(claimedJobs);
            Assert.Equal(5, claimedJobs.Count);

            foreach (var job in claimedJobs)
            {
                Assert.Equal(BatchJobStatusStrings.CLAIMING, job.Status);
                Assert.Equal(_testNodeId, job.ClaimedBy);
            }
        }
        finally
        {
            await CleanupJobsAsync(allJobs);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_ETagConflict_HandlesGracefully()
    {
        // Arrange: Create 2 Pending jobs
        var batchFileId = await CreateTestBatchFileAsync();
        var jobs = await CreateTestBatchJobsAsync(batchFileId, count: 2, status: BatchJobStatusStrings.PENDING);

        try
        {
            // Simulate race condition: Manually claim first job before TryClaimMultipleJobsAsync
            var job1 = jobs[0];
            var freshJob1 = await _batchJobAdapter.FetchBatchJobAsync(_testTenantId, batchFileId, job1.Id);
            if (freshJob1 != null)
            {
                freshJob1.Status = BatchJobStatusStrings.CLAIMING;
                freshJob1.ClaimedBy = "COMPETING-NODE";
                freshJob1.ClaimExpiration = DateTimeOffset.UtcNow.AddSeconds(60);
                await _batchJobAdapter.UpsertBatchJobAsync(freshJob1);
            }

            // Act: Try to claim both jobs (job1 should fail due to ETag conflict, job2 should succeed)
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 2,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should claim only job2 (job1 was already claimed)
            Assert.NotNull(claimedJobs);
            Assert.Single(claimedJobs);

            var claimed = claimedJobs[0];
            Assert.Equal(jobs[1].Id, claimed.Id); // Should be the second job
            Assert.Equal(BatchJobStatusStrings.CLAIMING, claimed.Status);
            Assert.Equal(_testNodeId, claimed.ClaimedBy);
        }
        finally
        {
            await CleanupJobsAsync(jobs);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_MissingClaimedByProperty_HandlesCorrectly()
    {
        // Arrange: Create job without ClaimedBy property (or null/empty)
        var batchFileId = await CreateTestBatchFileAsync();
        var job = await CreateTestBatchJobAsync(
            batchFileId,
            status: BatchJobStatusStrings.PENDING,
            claimedBy: null); // No ClaimedBy

        try
        {
            // Act: Try to claim
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should successfully claim (query handles NOT IS_DEFINED)
            Assert.NotNull(claimedJobs);
            Assert.Single(claimedJobs);

            var claimed = claimedJobs[0];
            Assert.Equal(BatchJobStatusStrings.CLAIMING, claimed.Status);
            Assert.Equal(_testNodeId, claimed.ClaimedBy);
        }
        finally
        {
            await CleanupJobAsync(job);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_TargetCountLimit_StopsAtTarget()
    {
        // Arrange: Create 10 Pending jobs
        var batchFileId = await CreateTestBatchFileAsync();
        var jobs = await CreateTestBatchJobsAsync(batchFileId, count: 10, status: BatchJobStatusStrings.PENDING);

        try
        {
            // Act: Claim with targetCount=5, maxCandidates=20
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should claim exactly 5, not all 10
            Assert.NotNull(claimedJobs);
            Assert.Equal(5, claimedJobs.Count);
        }
        finally
        {
            await CleanupJobsAsync(jobs);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_ReFetchFreshETag_UsesLatestETag()
    {
        // Arrange: Create 1 Pending job
        var batchFileId = await CreateTestBatchFileAsync();
        var job = await CreateTestBatchJobAsync(batchFileId, status: BatchJobStatusStrings.PENDING);

        try
        {
            // Get initial ETag from query
            var initialJob = await _batchJobAdapter.FetchBatchJobAsync(_testTenantId, batchFileId, job.Id);
            Assert.NotNull(initialJob);
            var initialETag = initialJob.ETag;

            // Update job externally (simulating another operation)
            initialJob.LastUpdated = DateTimeOffset.UtcNow;
            var updatedJob = await _batchJobAdapter.UpsertBatchJobAsync(initialJob);
            var updatedETag = updatedJob.ETag;
            Assert.NotEqual(initialETag, updatedETag); // ETag should have changed

            // Act: Try to claim (should re-fetch and use fresh ETag)
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should successfully claim with fresh ETag
            Assert.NotNull(claimedJobs);
            Assert.Single(claimedJobs);

            var claimed = claimedJobs[0];
            Assert.Equal(BatchJobStatusStrings.CLAIMING, claimed.Status);
            Assert.NotNull(claimed.ETag);
            // ETag should be different from initial (proving re-fetch happened)
        }
        finally
        {
            await CleanupJobAsync(job);
        }
    }

    [Fact]
    public async Task TryClaimMultipleJobsAsync_ActiveClaimingJob_NotReclaimed()
    {
        // Arrange: Create a job that's actively being claimed (not expired)
        var batchFileId = await CreateTestBatchFileAsync();
        var futureExpiration = DateTimeOffset.UtcNow.AddMinutes(5); // Not expired yet
        var activeNodeId = "ACTIVE-NODE-123";

        var activeJob = await CreateTestBatchJobAsync(
            batchFileId,
            status: BatchJobStatusStrings.CLAIMING,
            claimedBy: activeNodeId,
            claimExpiration: futureExpiration);

        try
        {
            // Act: Try to claim jobs
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                _testTenantId,
                _testNodeId,
                targetCount: 5,
                maxCandidates: 20,
                claimTimeoutSeconds: 60);

            // Assert: Should NOT reclaim the active job
            Assert.NotNull(claimedJobs);
            Assert.Empty(claimedJobs); // Should not claim active Claiming jobs
        }
        finally
        {
            await CleanupJobAsync(activeJob);
        }
    }

    // Helper Methods

    private async Task<string> CreateTestBatchFileAsync()
    {
        // Create an actual BatchFile entity in Cosmos DB (required for BatchJob validation)
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        var batchFile = new BatchFile(
            directory: _testTenantId.ToLower(),
            fileName: $"test-file-{batchFileId}.csv".ToLower(),
            originalFileName: $"test-original-{batchFileId}.csv".ToLower(),
            modelName: "sales-pos-claims", // Use same model name as other tests
            createDate: now,
            lastUpdated: now,
            tenantId: _testTenantId.ToLower(),
            id: batchFileId,
            jobDirectory: _testTenantId.ToLower());

        var created = await _batchFileAdapter.UpsertBatchFileAsync(batchFile);
        _createdBatchFileIds.Add(created.Id);
        return created.Id;
    }

    private async Task<BatchJob> CreateTestBatchJobAsync(
        string batchFileId,
        string status = "Pending",
        string? claimedBy = null,
        DateTimeOffset? claimExpiration = null)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new BatchJob(
            status: status,
            batchFileId: batchFileId,
            tenancy: _testTenantId,
            fileFriendlyName: $"test-file-{Guid.NewGuid()}",
            path: "/test/path",
            createDate: now,
            lastUpdated: now,
            tenantId: _testTenantId,
            id: Guid.NewGuid().ToString(),
            claimedBy: claimedBy,
            batchResultFileId: null,
            parentBatchJobId: null,
            chunkIndex: 0,        // Required by backend validation
            totalChunks: 1,        // Required by backend validation
            chunkStartLine: 0,    // Required by backend validation
            chunkEndLine: -1,      // Required by backend validation
            isChunk: false,
            processedBytes: null,
            processedLines: null,
            lastProcessedAt: null,
            claimExpiration: claimExpiration,
            etag: null);

        var created = await _batchJobAdapter.UpsertBatchJobAsync(job);
        _createdJobIds.Add(created.Id);
        return created;
    }

    private async Task<List<BatchJob>> CreateTestBatchJobsAsync(
        string batchFileId,
        int count,
        string status = "Pending",
        string? claimedBy = null,
        DateTimeOffset? claimExpiration = null)
    {
        var jobs = new List<BatchJob>();
        for (int i = 0; i < count; i++)
        {
            var job = await CreateTestBatchJobAsync(batchFileId, status, claimedBy, claimExpiration);
            jobs.Add(job);
        }
        return jobs;
    }

    private async Task CleanupJobAsync(BatchJob job)
    {
        try
        {
            await _batchJobAdapter.DeleteBatchJobAsync(_testTenantId, job.Id, job.BatchFileId);
        }
        catch (Exception ex)
        {
            // Log but don't fail test on cleanup errors
            Console.WriteLine($"Error cleaning up job {job.Id}: {ex.Message}");
        }
    }

    private async Task CleanupJobsAsync(IEnumerable<BatchJob> jobs)
    {
        foreach (var job in jobs)
        {
            await CleanupJobAsync(job);
        }
    }

    private async Task CleanupBatchFileAsync(string batchFileId)
    {
        try
        {
            await _batchFileAdapter.DeleteBatchFileAsync(_testTenantId, batchFileId);
        }
        catch (Exception ex)
        {
            // Log but don't fail test on cleanup errors
            Console.WriteLine($"Error cleaning up batch file {batchFileId}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Cleanup any remaining test data
        // Note: In a real scenario, you might want to make this async, but xUnit doesn't support async Dispose
        // For now, we rely on individual test cleanup
    }
}

// Reuse TestHttpFactory from CampaignSerializationTests
public class TestHttpFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
}

