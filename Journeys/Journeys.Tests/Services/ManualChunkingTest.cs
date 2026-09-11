using Journeys.Core.Services;
using Journeys.Core.Models;
using Journeys.Core.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Journeys.Core.Services.Ingest;

namespace Journeys.Tests.Services;

/// <summary>
/// Manual test class for testing chunking with real large files
/// Run this in your development environment with actual data
/// </summary>
public class ManualChunkingTest
{
    private readonly IngestService _ingestService;
    private readonly FileChunkingService _fileChunkingService;
    private readonly BatchProcessingOptions _options;

    public ManualChunkingTest(
        IngestService ingestService,
        FileChunkingService fileChunkingService,
        IOptions<BatchProcessingOptions> options)
    {
        _ingestService = ingestService;
        _fileChunkingService = fileChunkingService;
        _options = options.Value;
    }

    /// <summary>
    /// Test method to manually test chunking with your large order file
    /// </summary>
    public async Task TestWithLargeOrderFile()
    {
        // 1. First, test the chunking logic
        await TestChunkingLogic();

        // 2. Test the full ingest process
        await TestFullIngestProcess();

        // 3. Monitor the results
        await MonitorResults();
    }

    private async Task TestChunkingLogic()
    {
        Log.Information("=== Testing Chunking Logic ===");

        // Test with different chunk sizes
        var chunkSizes = new[] { 1024 * 1024, 2 * 1024 * 1024, 5 * 1024 * 1024 }; // 1MB, 2MB, 5MB

        foreach (var chunkSize in chunkSizes)
        {
            Log.Information($"Testing with chunk size: {chunkSize / (1024 * 1024)}MB");
            
            // Create a test batch job and file with matching IDs
            var (testBatchJob, testBatchFile) = CreateTestBatchJobAndFile();
            
            var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(
                testBatchJob, testBatchFile, chunkSize);

            Log.Information($"Created {chunkJobs.Count} chunk jobs");
            
            foreach (var job in chunkJobs)
            {
                Log.Information($"Chunk: {job.ChunkIndex + 1}/{job.TotalChunks}, " +
                              $"Lines: {job.ChunkStartLine}-{job.ChunkEndLine}, " +
                              $"File: {job.FileFriendlyName}");
            }
        }
    }

    private async Task TestFullIngestProcess()
    {
        Log.Information("=== Testing Full Ingest Process ===");

        // This would test the actual ingest process
        // You'd need to set up a real batch job with your large file
        
        Log.Information("Full ingest process test completed");
    }

    private async Task MonitorResults()
    {
        Log.Information("=== Monitoring Results ===");

        // Check result files, logs, etc.
        Log.Information("Results monitoring completed");
    }

    // Centralized test data
    private static class TestData
    {
        public const string Directory = ".";
        public const string FileName = "HaywardSalesPOSClaimFeed_20250610.csv";
        public const string OriginalFileName = "HaywardSalesPOSClaimFeed_20250610_orig.csv";
        public const string ModelName = "sales-pos-claims";
        public const string TenantId = "hayward";
        public const string Tenancy = "hayward";
        public const string Path = ".";
    }

    private (BatchJob, BatchFile) CreateTestBatchJobAndFile()
    {
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        var batchFile = new BatchFile(
            TestData.Directory,
            TestData.FileName,
            TestData.OriginalFileName,
            TestData.ModelName,
            now,
            now,
            TestData.TenantId,
            batchFileId,
            TestData.Directory
        );
        
        var batchJob = new BatchJob(
            BatchJobStatusStrings.PENDING,
            batchFileId, // This references the BatchFile.Id
            TestData.Tenancy,
            TestData.FileName, // File friendly name
            TestData.Path,
            now,
            now,
            TestData.TenantId,
            null,
            null,
            null,
            null, // ParentBatchJobId
            0, // ChunkIndex
            1, // TotalChunks
            0, // ChunkStartLine
            -1, // ChunkEndLine
            false, // IsChunk
            null, // ProcessedBytes
            null, // ProcessedLines
            null // LastProcessedAt
        );
        
        return (batchJob, batchFile);
    }

    private BatchJob CreateTestBatchJob()
    {
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        return new BatchJob(
            BatchJobStatusStrings.PENDING,
            batchFileId, // This references the BatchFile.Id
            TestData.Tenancy,
            TestData.FileName, // File friendly name
            TestData.Path,
            now,
            now,
            TestData.TenantId,
            null,
            null,
            null,
            null, // ParentBatchJobId
            0, // ChunkIndex
            1, // TotalChunks
            0, // ChunkStartLine
            -1, // ChunkEndLine
            false, // IsChunk
            null, // ProcessedBytes
            null, // ProcessedLines
            null // LastProcessedAt
        );
    }

    private BatchFile CreateTestBatchFile()
    {
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        return new BatchFile(
            TestData.Directory,
            TestData.FileName,
            TestData.OriginalFileName,
            TestData.ModelName,
            now,
            now,
            TestData.TenantId,
            batchFileId,
            TestData.Directory
        );
    }
} 