using Journeys.Core.RulesEngine.Engine;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IEventService
    {
        Task ProcessBulk(string tenantId, string modelName, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 10);
        Task<EventPayloadResponseDto> GetAccountEntity(string tenantId, string modelName, string id, CancellationToken token, bool includeChildModels);
        Task<PagedResultSetResponse<EventPayloadResponseDto>> QueryAsync(string tenantId, string modelName, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder,  int pageSize, string? continuationToken, CancellationToken token, bool includeChildModels);
        Task<EventPayloadResponseDto> ProcessEventAsync(string tenantId, string modelName, JsonElement data, CancellationToken? token = null, bool reprocessEvent = false, string? campaignId = null);

        /// <summary>
        /// CAUTION: Admin function
        /// </summary>
        /// <param name="tenantId"></param>
        /// <param name="modelName"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        Task<EventPayloadResponseDto> SaveEventPayload(string tenantId, string modelName, EventPayloadResponseDto dto);

        Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByFileAsync(string tenantId, string schemaName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false);

        Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByXidAsync(string tenantId, string schemaName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false);
        Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsAsync(string tenantId, string schemaName, ReconcileAccountRequest request, CancellationToken? token = null);

        Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, string accountId, CancellationToken? token = null);
        Task<LoyaltyAccountDto> ResettleAccountByXidAsync(string tenantId, string extRefId, CancellationToken? token = null);
        Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, LoyaltyAccountDto account, CancellationToken? token = null);

    }
}
