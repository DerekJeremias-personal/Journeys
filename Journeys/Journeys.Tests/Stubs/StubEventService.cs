using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System.Text.Json;

namespace Journeys.Tests.Stubs;

public class StubEventService : IEventService
{
    public Task ProcessBulk(string tenantId, string modelName, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 10)
    {
        return Task.CompletedTask;
    }

    public Task<EventPayloadResponseDto> GetAccountEntity(string tenantId, string modelName, string id, CancellationToken token, bool includeChildModels)
    {
        return Task.FromResult<EventPayloadResponseDto>(null!);
    }

    public Task<PagedResultSetResponse<EventPayloadResponseDto>> QueryAsync(string tenantId, string modelName, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, string? continuationToken, CancellationToken token, bool includeChildModels)
    {
        return Task.FromResult<PagedResultSetResponse<EventPayloadResponseDto>>(null!);
    }

    public Task<EventPayloadResponseDto> ProcessEventAsync(string tenantId, string modelName, JsonElement data, CancellationToken? token = null, bool reprocessEvent = false, string? campaignId = null)
    {
        return Task.FromResult<EventPayloadResponseDto>(null!);
    }

    public Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByXidAsync(string tenantId, string schemaName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false)
    {
        throw new NotImplementedException();
    }

    public Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsAsync(string tenantId, string schemaName, ReconcileAccountRequest request, CancellationToken? token = null)
    {
        throw new NotImplementedException();
    }

    public Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByFileAsync(string tenantId, string schemaName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false)
    {
        throw new NotImplementedException();
    }

    public Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, string accountId, CancellationToken? token = null)
    {
        throw new NotImplementedException();
    }

    public Task<LoyaltyAccountDto> ResettleAccountByXidAsync(string tenantId, string extRefId, CancellationToken? token = null)
    {
        throw new NotImplementedException();
    }

    public Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, LoyaltyAccountDto account, CancellationToken? token = null)
    {
        throw new NotImplementedException();
    }

    public Task<EventPayloadResponseDto> SaveEventPayload(string tenantId, string modelName, EventPayloadResponseDto dto)
    {
        throw new NotImplementedException();
    }
}