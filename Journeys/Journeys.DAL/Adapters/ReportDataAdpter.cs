using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Infrastructure.DataStorage
{
    public class ReportDataAdapter : IReportDataAdapter
    {
        private readonly IDynamicDataAdapter _dynamicDataAdapter;

        private const string LOYALTY_CONTAINER_MODEL_ID = "e2cc2404-c60b-4cbc-9f50-6db7ee58e01d";
        private const string POINT_CONTAINER_MODEL_ID = "56ff2535-f1d9-4de7-a0db-67ed2087063a";
        private const string LOYALTY_RULE_CONTAINER_MODEL_ID = "a79bcc89-07cd-4ae9-87e9-95c45aa48819";
        private const string ORDER_AND_RULE_CONTAINER_MODEL_ID = "f00df00d-dead-f00d-f00d-ea7f00d1337e";

        public ReportDataAdapter(IDynamicDataAdapter dynamicDataAdapter, ILogger<ReportDataAdapter> logger)
        {
            _dynamicDataAdapter = dynamicDataAdapter;
        }

        public async Task<PagedResultSet<LoyaltyAccount>> GetAccountsAsync(string tenantId, int pageSize,string continuationToken, CancellationToken token = default)
        {
            var request = new GetByQueryRequest();

            var result = await _dynamicDataAdapter.QueryEntitiesAsync<LoyaltyAccount>(
                tenantId,
                LOYALTY_CONTAINER_MODEL_ID,
                query: "",
                parameters: request.Parameters,
                sortBy: request.SortBy,
                sortOrder: request.SortOrder,
                pageSize: pageSize,
                token: token,
                continuationToken: continuationToken,
                includeChildModels: true
            );

            return result;
        }

        public async Task<List<LoyaltyAccountRuleState>> GetDetailedAccountsAsync(string tenantId, List<string> accountids,int pageSize, CancellationToken token = default)
        {
            var request = new GetByQueryRequest();
            var condition = string.Join(" OR ", accountids.Select(c => $"c.accountid = \"{c}\""));
            var result = await _dynamicDataAdapter.QueryEntitiesAsync<LoyaltyAccountRuleState>(
                tenantId,
                LOYALTY_RULE_CONTAINER_MODEL_ID,
                query: condition,
                parameters: request.Parameters,
                sortBy: request.SortBy,
                sortOrder: request.SortOrder,
                pageSize: pageSize,
                token: token,
                continuationToken: request.ContinuationToken,
                includeChildModels: true
            );

            return result.Entities;
        }


        public async Task<List<PointLedger>> GetPointLedgersAsync(string tenantId, List<string> accountids, int pageSize, CancellationToken token = default)
        {
            var request = new GetByQueryRequest();
            var condition = string.Join(" OR ", accountids.Select(c => $"c.accountid = \"{c}\""));
            var result = await _dynamicDataAdapter.QueryEntitiesAsync<PointLedger>(
                tenantId,
                POINT_CONTAINER_MODEL_ID,
                query: condition,
                parameters: request.Parameters,
                sortBy: request.SortBy,
                sortOrder: request.SortOrder,
                pageSize: pageSize,
                token: token,
                continuationToken: request.ContinuationToken,
                includeChildModels: true
            );

            return result.Entities;
        }
        public async Task<List<OrderAndRuleStateDto>> GetOrdersAsync(
        string tenantId,
        List<string> accountIds,
        int pageSize = 2000,
        CancellationToken token = default)
        {
            return await DrainAllAsync<OrderAndRuleStateDto>(async continuationToken =>
            {
                var request = new GetByQueryRequest();
                var condition = string.Join(" OR ",
                    accountIds.Select(c => $"c.accountid = \"{c}\""));

                return await _dynamicDataAdapter.QueryEntitiesAsync<OrderAndRuleStateDto>(
                    tenantId,
                    ORDER_AND_RULE_CONTAINER_MODEL_ID,
                    query: condition,
                    parameters: request.Parameters,
                    sortBy: request.SortBy,
                    sortOrder: request.SortOrder,
                    pageSize: pageSize,
                    token: token,
                    continuationToken: continuationToken,
                    includeChildModels: false
                );
            });
        }

        private async Task<List<T>> DrainAllAsync<T>(Func<string, Task<PagedResultSet<T>>> fetchPage)
        {
            var results = new List<T>();
            string continuationToken = null;

            do
            {
                var page = await fetchPage(continuationToken);

                if (page?.Entities != null)
                    results.AddRange(page.Entities);

                continuationToken = page?.ContinuationToken;

            } while (!string.IsNullOrEmpty(continuationToken));

            return results;
        }

    }
}
