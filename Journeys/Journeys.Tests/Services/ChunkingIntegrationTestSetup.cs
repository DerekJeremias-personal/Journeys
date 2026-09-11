using Journeys.Core.Services;
using Journeys.Core.Models;
using Journeys.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Infra.DataLake;
using Journeys.DAL.Adapters;
using Xunit;
using Journeys.Core.Services.Ingest;

namespace Journeys.Tests.Services;

/// <summary>
/// Integration test setup for chunking functionality
/// This class can be used to set up proper dependency injection for testing
/// </summary>
public class ChunkingIntegrationTestSetup
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FileChunkingService _fileChunkingService;
    private readonly ParallelBatchProcessorService _parallelBatchProcessor;
    private readonly BatchProcessingOptions _options;

    public ChunkingIntegrationTestSetup()
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

        // Add your actual services here
        services.AddScoped<IDataLakeAdapter, DataLakeAdapter>();
        services.AddScoped<IBatchJobAdapter, BatchJobAdapter>();
        services.AddScoped<IBatchFileAdapter, BatchFileAdapter>();
        services.AddScoped<ResumabilityService>();
        services.AddScoped<FileChunkingService>();
        services.AddScoped<ParallelBatchProcessorService>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Now get the actual services
        _options = _serviceProvider.GetRequiredService<IOptions<BatchProcessingOptions>>().Value;
        _fileChunkingService = _serviceProvider.GetRequiredService<FileChunkingService>();
        _parallelBatchProcessor = _serviceProvider.GetRequiredService<ParallelBatchProcessorService>();
    }

    /// <summary>
    /// Test method that can be run when proper services are configured
    /// </summary>
    public async Task TestChunkingWithRealServices()
    {
        // Create test data
        var (batchJob, batchFile) = CreateTestBatchJobAndFile();
        
        // Test chunking
        var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(
            batchJob, batchFile, 1024 * 1024); // 1MB chunks
        
        // Verify results
        Assert.NotNull(chunkJobs);
        Assert.True(chunkJobs.Count > 0, "Should create at least one job");
        
        // Log the results
        Console.WriteLine($"Created {chunkJobs.Count} chunk jobs");
        foreach (var job in chunkJobs)
        {
            Console.WriteLine($"Job: {job.Id}, IsChunk: {job.IsChunk}, ChunkIndex: {job.ChunkIndex}");
        }
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
} 