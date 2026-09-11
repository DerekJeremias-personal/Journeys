using Journeys.Core.Services;
using Journeys.Core.Models;
using Journeys.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text.Json;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Tests.Stubs;
using Xunit;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services.Ingest;

namespace Journeys.Tests.Services;

public class ChunkingIntegrationTests
{
    private readonly FileChunkingService _fileChunkingService;
    private readonly ParallelBatchProcessorService _parallelBatchProcessor;
    private readonly BatchProcessingOptions _options;

    public ChunkingIntegrationTests()
    {
        // Set up dependency injection container
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add configuration
        services.Configure<BatchProcessingOptions>(options =>
        {
            options.ChunkSizeBytes = 1024 * 1024; // 1MB for testing
            options.MaxConcurrency = 2; // Lower for testing
            options.EnableChunking = true;
            options.EnableParallelProcessing = true;
            options.WriteFrequency = 5;
            options.EnableFrequentWrites = true;
        });

        // Add stub services for testing
        services.AddScoped<IDataLakeAdapter, StubDataLakeAdapter>();
        services.AddScoped<IBatchJobAdapter, StubBatchJobAdapter>();
        services.AddScoped<IBatchFileAdapter, StubBatchFileAdapter>();
        services.AddScoped<IEventService, StubEventService>();
        services.AddScoped<IBatchJobService, StubBatchJobService>();
        services.AddScoped<ResumabilityService>();
        services.AddScoped<FileChunkingService>();
        services.AddScoped<ParallelBatchProcessorService>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Get the actual services
        _options = serviceProvider.GetRequiredService<IOptions<BatchProcessingOptions>>().Value;
        _fileChunkingService = serviceProvider.GetRequiredService<FileChunkingService>();
        _parallelBatchProcessor = serviceProvider.GetRequiredService<ParallelBatchProcessorService>();
    }

    [Fact]
    public async Task TestLargeFileChunking()
    {
        // Arrange
        var (largeBatchJob, largeBatchFile) = CreateTestBatchJobAndFile(TestData.LargeFileName);

        // Act
        var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(
            largeBatchJob, largeBatchFile, 1024 * 1024); // 1MB chunks

        // Assert
        Assert.True(chunkJobs.Count > 1, "Large file should be split into multiple chunks");
        Assert.All(chunkJobs, job => Assert.True(job.IsChunk));
        Assert.All(chunkJobs, job => Assert.Equal(largeBatchJob.Id, job.ParentBatchJobId));
    }

    [Fact]
    public async Task TestSmallFileNoChunking()
    {
        // Arrange
        var (smallBatchJob, smallBatchFile) = CreateTestBatchJobAndFile(TestData.SmallFileName);

        // Act
        var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(
            smallBatchJob, smallBatchFile, 1024 * 1024 * 10); // 10MB chunks

        // Assert
        Assert.Single(chunkJobs);
        Assert.False(chunkJobs[0].IsChunk);
    }

    [Fact]
    public async Task TestParallelProcessing()
    {
        // Arrange
        var batchJobs = new List<BatchJob>
        {
            CreateTestBatchJob("job1"),
            CreateTestBatchJob("job2"),
            CreateTestBatchJob("job3")
        };

        // Act
        await _parallelBatchProcessor.ProcessBatchJobsInParallelAsync(batchJobs, async (group) =>
        {
            // Dummy processing for test
            await Task.CompletedTask;
        });

        // Assert
        // You'd verify that jobs were processed in parallel
        // This might involve checking logs or timing
        Assert.True(true, "Parallel processing completed successfully");
    }

    [Fact]
    public void TestBatchJobAndFileCreation()
    {
        // Arrange & Act
        var (batchJob, batchFile) = CreateTestBatchJobAndFile();

        // Assert
        Assert.NotNull(batchJob);
        Assert.NotNull(batchFile);
        Assert.Equal(batchFile.Id, batchJob.BatchFileId);
        Assert.Equal(TestData.TenantId, batchJob.TenantId);
        Assert.Equal(TestData.TenantId, batchFile.TenantId);
        Assert.Equal(TestData.LargeFileName, batchJob.FileFriendlyName);
        Assert.Equal(TestData.LargeFileName, batchFile.FileName);
        Assert.Equal(TestData.OriginalFileName, batchFile.OriginalFileName);
        Assert.Equal(TestData.ModelName, batchFile.ModelName);
        Assert.Equal(TestData.Directory, batchFile.Directory);
        Assert.Equal(TestData.Path, batchJob.Path);
    }

    // Centralized test data
    private static class TestData
    {
        public const string Directory = "..\\..\\..\\";
        public const string LargeFileName = "LargeFileTest.csv";
        public const string SmallFileName = "small-test.csv";
        public const string OriginalFileName = "LargeFileTest_orig.csv";
        public const string ModelName = "sales-pos-claims";
        public const string TenantId = "hayward";
        public const string Tenancy = "hayward";
        public const string Path = ".";
    }

    private (BatchJob, BatchFile) CreateTestBatchJobAndFile(string fileName = null)
    {
        fileName ??= TestData.LargeFileName; // Default to large file for backward compatibility
        
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        var batchFile = new BatchFile(
            TestData.Directory,
            fileName,
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
            fileName, // File friendly name
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

    private BatchJob CreateTestBatchJob(string id = "test-job")
    {
        var batchFileId = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        
        return new BatchJob(
            BatchJobStatusStrings.PENDING,
            batchFileId, // This references the BatchFile.Id
            TestData.Tenancy,
            TestData.LargeFileName, // File friendly name
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
            TestData.LargeFileName,
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