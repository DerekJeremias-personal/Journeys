using System.Text.Json;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DAL.Adapters;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public sealed class AgentMessageAdapterLoadTests
{
    [Fact]
    public async Task LoadConversationForAuditAsync_happy_path_returns_chat_and_workflow()
    {
        const string tenant = "primo";
        const string owner = "user1";
        const string conv = "4073a72a920543959bddef2b10900b5b";
        var probeRow = Message(tenant, owner, conv, 1, "user", "hi");
        var chatRows = new List<AgentMessage> { probeRow };
        var workflow = Message(
            tenant, owner, conv, 0, AgentMessage.WorkflowOrchestrationRole, "{}",
            id: AgentMessage.WorkflowOrchestrationDocumentId);

        var fake = new SealedFakeDynamicDataAdapter(
            queryResult: new PagedResultSet<AgentMessage> { Entities = [probeRow] },
            pkResult: new PagedResultSet<AgentMessage> { Entities = chatRows },
            entityResult: workflow);

        var adapter = new AgentMessageAdapter(fake, new ConfigurationBuilder().Build());
        var load = await adapter.LoadConversationForAuditAsync(tenant, "4073A72A-9205-4395-9BDD-EF2B10900B5B");

        Assert.Equal(owner, load.OwnerUserId);
        Assert.Equal(conv, load.ConversationId);
        Assert.Single(load.ChatMessages);
        Assert.NotNull(load.WorkflowRow);
    }

    [Fact]
    public async Task LoadConversationForAuditAsync_not_found_throws()
    {
        var fake = new SealedFakeDynamicDataAdapter(
            queryResult: new PagedResultSet<AgentMessage> { Entities = [] });
        var adapter = new AgentMessageAdapter(fake, new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.LoadConversationForAuditAsync("primo", "missingid123456789012345678901234"));
        Assert.Contains("No AgentMessage rows found", ex.Message);
    }

    [Fact]
    public async Task LoadConversationForAuditAsync_conflicting_owners_throws()
    {
        var row1 = Message("primo", "user1", "abc123456789012345678901234567890", 1, "user", "a");
        var row2 = Message("primo", "user2", "abc123456789012345678901234567890", 2, "user", "b");
        var fake = new SealedFakeDynamicDataAdapter(
            queryResult: new PagedResultSet<AgentMessage> { Entities = [row1, row2] });
        var adapter = new AgentMessageAdapter(fake, new ConfigurationBuilder().Build());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.LoadConversationForAuditAsync("primo", "abc123456789012345678901234567890"));
        Assert.Contains("conflicting ownerUserId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentMessage Message(
        string tenantId,
        string ownerUserId,
        string conversationId,
        long sequence,
        string role,
        string content,
        string? id = null) =>
        new(
            tenantId,
            ownerUserId,
            conversationId,
            sequence,
            role,
            content,
            linkedCampaignId: null,
            toolCallId: null,
            toolName: null,
            toolArgumentsJson: null,
            toolResultJson: null,
            id: id);

    private sealed class SealedFakeDynamicDataAdapter : IDynamicDataAdapter
    {
        private readonly PagedResultSet<AgentMessage> _queryResult;
        private readonly PagedResultSet<AgentMessage> _pkResult;
        private readonly AgentMessage? _entityResult;

        public SealedFakeDynamicDataAdapter(
            PagedResultSet<AgentMessage> queryResult,
            PagedResultSet<AgentMessage>? pkResult = null,
            AgentMessage? entityResult = null)
        {
            _queryResult = queryResult;
            _pkResult = pkResult ?? new PagedResultSet<AgentMessage> { Entities = [] };
            _entityResult = entityResult;
        }

        public JsonSerializerOptions GetJsonSerializerOptions() => new();

        public Task<T?> GetEntityAsync<T>(
            string tenantid,
            string id,
            string modelId,
            string pk = null,
            string pk2 = null,
            JsonSerializerOptions serializerOptions = null)
        {
            if (typeof(T) != typeof(AgentMessage))
                throw new NotSupportedException();
            return Task.FromResult((T)(object)_entityResult!);
        }

        public Task<PagedResultSet<T>> GetEntitiesByPKAsync<T>(
            string tenantId,
            string partitionKey,
            string modelId,
            int pageSize,
            string pk2 = null,
            string? continuationToken = null,
            JsonSerializerOptions serializerOptions = null)
        {
            if (typeof(T) != typeof(AgentMessage))
                throw new NotSupportedException();
            return Task.FromResult((PagedResultSet<T>)(object)_pkResult);
        }

        public Task<PagedResultSet<T>> QueryEntitiesAsync<T>(
            string tenantId,
            string modelId,
            string query,
            Dictionary<string, object> parameters,
            string sortBy,
            SortOrder sortOrder,
            int pageSize,
            CancellationToken token = default,
            string continuationToken = null,
            JsonSerializerOptions serializerOptions = null,
            bool includeChildModels = false)
        {
            if (typeof(T) != typeof(AgentMessage))
                throw new NotSupportedException();
            return Task.FromResult((PagedResultSet<T>)(object)_queryResult);
        }

        public Task<List<T>> GetManyEntitiesAsync<T>(
            string tenantId,
            List<string> ids,
            string modelId,
            Type? entityType = default,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<List<T>> GetManyEntitiesAsync<T>(
            string tenantId,
            List<(string, Dictionary<string, string>)> ids,
            string modelId,
            Type? entityType = default,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<PagedResultSet<T>> GetAllEntitiesAsync<T>(
            string tenantId,
            string modelId,
            int pageSize,
            string? continuationToken = null,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<T> SetEntityAsync<T>(
            string tenantId,
            T entity,
            string modelId,
            Type? entityType = default,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<object> SetLogicalEntityAsync(
            string tenantId,
            JsonElement entity,
            string modelId,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<T> MoveEntityAsync<T>(
            string tenantId,
            T entity,
            Dictionary<string, string> newPartition,
            string modelId,
            Type? entityType = default,
            JsonSerializerOptions serializerOptions = null) =>
            throw new NotImplementedException();

        public Task<bool> RemoveEntityAsync(
            string tenantId,
            string id,
            string modelId,
            Dictionary<string, string> pks = null) =>
            throw new NotImplementedException();

        public Task<bool> RemoveEntityAsync<T>(string tenantId, T entity, string scehmaId) =>
            throw new NotImplementedException();

        public void SetPolymorphicTypeMap(Dictionary<string, Type> map, string typeMapPropertyName) { }
    }
}
