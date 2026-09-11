using Journeys.Core.Models;
using Journeys.DTO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Journeys.Core.Services.ReportService;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IReportDataAdapter
    {
        Task<PagedResultSet<LoyaltyAccount>> GetAccountsAsync(string tenantId, int pageSize, string continuationToken, CancellationToken token = default);
        Task<List<LoyaltyAccountRuleState>> GetDetailedAccountsAsync(string tenantId, List<string> accountids, int pageSize, CancellationToken token = default);
        Task<List<PointLedger>> GetPointLedgersAsync(string tenantId, List<string> accountids, int pageSize, CancellationToken token = default);
        Task<List<OrderAndRuleStateDto>> GetOrdersAsync(string tenantId, List<string> accountids,int pageSize, CancellationToken token = default);
    }
}
