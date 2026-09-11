using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses.BlobResponses
{
    public class IngestFileResults : ResponseBase
    {
        public string BatchFileId { get; set; }
        public string Directory { get; set; }
        public string FileType { get; set; }
        public string OriginalfileName { get; set; }
        public DateTimeOffset ProcessDate { get; set; }
        public int TotalFileLines { get; set; }
        public int TotalFileChunks { get; set; }
        public int ErrorCount { get; set; }
        public int RunTimespanMinutes { get; set; }
        public decimal FileSize { get; set; }
        public List<IngestFileChunkResult> IngestFileChunkResults { get; set; }
        public List<IngestFileError> IngestFileErrors { get; set; }
    }

    public class IngestFileChunkResult
    {
        public string BatchFileId { get; set; }
        public string BatchJobId { get; set; }
        public string Status { get; set; }
        public string OriginalfileName { get; set; }
        public DateTimeOffset ProcessDate { get; set; }
        public int ChunkIndex { get; set; }
        public int StartLineNumber { get; set; }
        public int EndLineNumber { get; set; }
        public int ProcessedLines { get; set; }
        public decimal ProcessedBytes { get; set; }
    }

    public class IngestFileError
    {
        public int OriginalFileLineNumber { get; set; }
        public string Error { get; set; }
        public string ErrorMessage { get; set; }

        public string LineKey { get; set; }
        public string LineData { get; set; }

    }

}
