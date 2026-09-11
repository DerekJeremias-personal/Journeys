using Journeys.Core.Interfaces.Entities;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models
{
    public class FileIngestionSummary : TenantedModelBase, IReferenceable
    {
        [JsonConstructor]
        public FileIngestionSummary(
            string tenantId,
            string id,
            string fileName,
            string directoryLoaded,
            int totalRows,
            int totalSuccess,
            int totalErrors,
            DateTimeOffset? dateFileDropped = null,
            DateTimeOffset? dateFileProcessed = null,
            string completionTime = null,
            string fileSize = null,
            string source = null,
            string referenceId = null,
            string fileType = null,
            string status = null,
            bool @checked = false,
            DateTimeOffset? createDate = null,
            DateTimeOffset? lastUpdated = null
        ) : base(tenantId, id)
        {
            FileName = fileName;
            DirectoryLoaded = directoryLoaded;
            TotalRows = totalRows;
            TotalSuccess = totalSuccess;
            TotalErrors = totalErrors;
            DateFileDropped = dateFileDropped ?? DateTimeOffset.UtcNow;
            DateFileProcessed = dateFileProcessed;
            CompletionTime = completionTime;
            FileSize = fileSize;
            Source = source;
            ReferenceId = referenceId;
            FileType = fileType;
            Status = status;
            Checked = @checked;
            CreateDate = createDate ?? DateTimeOffset.UtcNow;
            LastUpdated = lastUpdated ?? DateTimeOffset.UtcNow;
        }
        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalId => Id;

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalIdType => "FileIngestionSummary";

        public string FileName { get; set; }
        public string DirectoryLoaded { get; set; }
        public int TotalRows { get; set; }
        public int TotalSuccess { get; set; }
        public int TotalErrors { get; set; }
        public DateTimeOffset? DateFileDropped { get; set; }
        public DateTimeOffset? DateFileProcessed { get; set; }
        public string CompletionTime { get; set; }
        public string FileSize { get; set; }
        public string Source { get; set; }
        public string ReferenceId { get; set; }
        public string FileType { get; set; }
        public string Status { get; set; }
        public bool Checked { get; set; }
        public DateTimeOffset? CreateDate { get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
    }
}