using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class SegmentDto : DtoModelBase
    {
        public string ExtSegmentId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string? TargetFolder { get; set; }
        public string? TargetFile { get; set; }
        public DateTimeOffset? FileCreateDate { get; set; }
        public SegmentSourceDto? Source { get; set; }
        public ScheduleDto? Schedule { get; set; }
    }

    public class SegmentSourceDto
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string? SourceFolder { get; set; }
        public string? SourceFile { get; set; }
        public string? ApiUrl { get; set; }
        public string? Query { get; set; }
        public DateTimeOffset? LastRunDate { get; set; }
        public decimal? LastRunDuration { get; set; }

    }

}
