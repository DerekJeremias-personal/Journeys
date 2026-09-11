using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class FileSummaryDto
    {
        public string FileName { get; set; }
        public DateTimeOffset DateIngested { get; set; }
        public string FileSize { get; set; }
        public string FileType { get; set; }

        public string Status { get; set; }

        public decimal? TotalRows { get; set; }
        public decimal? TotalErrors { get; set; }
        public decimal? TotalProcessedLines { get; set; }

        public DateTimeOffset? DateFileProcessingStarted { get; set; }
        public DateTimeOffset? DateFileProcessingFinished { get; set; }


    }
}
