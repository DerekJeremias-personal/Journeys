using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class FileIngestionSummaryDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; }
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
    }
}
