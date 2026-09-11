using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.Core.Models
{
    public class Segment : ModelBase
    {
        [JsonConstructor]
        public Segment(string extSegmentId, string name, string status, string type,
            string targetFolder, string targetFile, DateTimeOffset? fileCreateDate,
            SegmentSource? source, Schedule? schedule, string id) : base(id)
        {
            ExtSegmentId = extSegmentId;
            Name = name;
            Status = status;
            Type = type;
            TargetFolder = targetFolder;
            TargetFile = targetFile;
            FileCreateDate = fileCreateDate;
            Source = source;
            Schedule = schedule;
        }

        public string ExtSegmentId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string TargetFolder { get; set; }
        public string TargetFile { get; set; }
        public DateTimeOffset? FileCreateDate { get; set; }
        public SegmentSource? Source { get; set; }
        public Schedule? Schedule { get; set; }


    }

    public class SegmentSource
    {
        [JsonConstructor]
        public SegmentSource(string name, string status, string type,
            string? sourceFolder, string? sourceFile, string? apiUrl,
            string? query, DateTimeOffset? lastRunDate, decimal? lastRunDuration)
        {
            Name = name;
            Status = status;
            Type = type;
            SourceFolder = sourceFolder;
            SourceFile = sourceFile;
            ApiUrl = apiUrl;
            Query = query;
            LastRunDate = lastRunDate;
            LastRunDuration = lastRunDuration;
        }

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
