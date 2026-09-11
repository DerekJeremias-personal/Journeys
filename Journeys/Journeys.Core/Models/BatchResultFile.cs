using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

public class BatchResultFile : TenantedModelBase
{
    [JsonConstructor]
    public BatchResultFile(string directory, string batchJobId, string fileName, 
            DateTimeOffset createDate, DateTimeOffset lastUpdated, string tenantId, string? id) : base(tenantId, id)
    {
        Directory = directory;
        BatchJobId = batchJobId;
        FileName = fileName;
        CreateDate = createDate;
        LastUpdated = lastUpdated;
    }

    [JsonIgnore]
    public string? ModelId { get; set; }

    //public string? Status { get; set; }

    public string Directory { get; set; }

    public string BatchJobId { get; set; }
    
    public string FileName { get; set; }

    public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
