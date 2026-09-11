using Journeys.Core.Models;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ILoyaltyAccountPointLedgerAdapter
    {
        Task<PagedResultSet<PointLedger>> FetchLoyaltyAccountLedgersAsync(string tenantId, string accountId, int pageSize, int? pageNumber = null, string? continuationToken = null);

        Task<List<PointLedger>> GetLoyaltyAccountLedgersAsync(string tenantId, List<string> filters);

        Task<PointLedger> UpsertLoyaltyAccountLedgerAsync(string tenantId, PointLedger ledger);

        Task DeleteLoyaltyAccountLedgerAsync(string tenantId, string loyaltyAccountId, string id);
    }
}
