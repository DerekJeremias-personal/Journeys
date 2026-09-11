using System.Collections.Generic;

namespace Journeys.DTO.Requests
{
    public class ExportByQueryRequest
    {
        public string QueryString { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<ExportColumn> Columns { get; set; }
    }

    public class ExportColumn
    {
        public string Header { get; set; }
        public string Field { get; set; }
    }
} 