using Journeys.Core.Models;
using Journeys.DTO.Models.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IReportService
    {
        Task<ReportResult> ProcessDailyReport(string tenantId);
    }
}
