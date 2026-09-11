using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Cronos;

public class BlobArchiveJobProcessor : BackgroundService
{
    private readonly ILogger<BlobArchiveJobProcessor> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _config;

    public BlobArchiveJobProcessor(
        ILogger<BlobArchiveJobProcessor> logger,
        BlobServiceClient blobServiceClient,
        IConfiguration config)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var jobsSection = _config.GetSection("BlobArchiveJob");

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var job in jobsSection.GetChildren())
            {
                await RunJob(job, stoppingToken);
            }

            // Prevent tight loop – minimum wait
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunJob(IConfigurationSection jobConfig, CancellationToken ct)
    {
        var cron = CronExpression.Parse(jobConfig["Cron"]!);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(jobConfig["TimeZone"]!);

        var nextRun = cron.GetNextOccurrence(DateTimeOffset.Now, timeZone);
        if (!nextRun.HasValue)
            return;

        var delay = nextRun.Value - DateTimeOffset.Now;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct);

        await RunArchiveJob(jobConfig, ct);
    }

    private async Task RunArchiveJob(IConfigurationSection jobConfig, CancellationToken ct)

    {
        try
        {
            var jobName = jobConfig.Key;

            _logger.LogInformation("Archive job started: {Job}", jobName);

            var containerName = jobConfig["ContainerName"]!;
            var rootPrefix = jobConfig["RootPrefix"]!;
            var archiveFolderName = jobConfig["ArchiveFolderName"]!;
            var archiveAfterDays = int.Parse(jobConfig["ArchiveAfterDays"]!);

            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-archiveAfterDays);
            var container = _blobServiceClient.GetBlobContainerClient(containerName);

            await foreach (var item in container.GetBlobsByHierarchyAsync(
                prefix: rootPrefix,
                delimiter: "/",
                cancellationToken: ct))
            {
                if (!item.IsPrefix)
                    continue;

                var folderPrefix = item.Prefix!;

                if (folderPrefix.EndsWith($"/{archiveFolderName}/"))
                    continue;

                await ArchiveFolder(
                    container,
                    folderPrefix,
                    archiveFolderName,
                    cutoffDate,
                    ct);
            }

            _logger.LogInformation("Archive job completed: {Job}", jobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Archive job failed: {Job}", jobConfig.Key);
        }
    }

    private async Task ArchiveFolder(
        BlobContainerClient container,
        string folderPrefix,
        string archiveFolderName,
        DateTimeOffset cutoffDate,
        CancellationToken ct)
    {
        folderPrefix = folderPrefix.TrimEnd('/') + "/";
        var archivePrefix = $"{folderPrefix}{archiveFolderName}/";

        await foreach (var item in container.GetBlobsByHierarchyAsync(
            prefix: folderPrefix,
            delimiter: "/",
            cancellationToken: ct))
        {
            // Skip any subfolders (including archive/)
            if (item.IsPrefix)
                continue;

            var blob = item.Blob;

            // Skip recent files
            if (!blob.Properties.LastModified.HasValue ||
                blob.Properties.LastModified.Value >= cutoffDate)
                continue;

            var sourceBlob = container.GetBlobClient(blob.Name);

            var archiveBlobName = $"{archivePrefix}{Path.GetFileName(blob.Name)}";
            var archiveBlob = container.GetBlobClient(archiveBlobName);

            _logger.LogInformation(
                "Archiving file {Source} → {Archive}",
                blob.Name,
                archiveBlobName);

            await archiveBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: ct);

            while (true)
            {
                var props = await archiveBlob.GetPropertiesAsync(cancellationToken: ct);

                if (props.Value.CopyStatus == CopyStatus.Success)
                    break;

                if (props.Value.CopyStatus is CopyStatus.Failed or CopyStatus.Aborted)
                    throw new Exception($"Copy failed: {blob.Name}");

                await Task.Delay(500, ct);
            }

            await sourceBlob.DeleteAsync(cancellationToken: ct);
        }
    }

}