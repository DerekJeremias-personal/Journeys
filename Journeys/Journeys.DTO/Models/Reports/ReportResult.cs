using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.Reports
{
    public class ReportResult
    {
        public string CurrentDateTime { get; set; }
        public string SnapshotDateEndOfDay { get; set; }
        public List<object> customersReportJson { get; set; } = new();
        public List<object> nonOrderReportJson { get; set; } = new();
    }
}
