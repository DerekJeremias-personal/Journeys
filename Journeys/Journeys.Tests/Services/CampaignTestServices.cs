using Backend.Dto.Structures.Model;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.Services;

internal static class CampaignTestServices
{
    public static CampaignTaxonomyRuleValidator CreateTaxonomyValidator(IModelAdapter? modelAdapter = null) =>
        new(
            modelAdapter ?? new CampaignTestStubModelAdapter(),
            Options.Create(new CampaignUpsertValidationOptions()),
            new CampaignTestNopLogger<CampaignTaxonomyRuleValidator>());

    public static CampaignJourneyPointOutcomePatValidator CreatePointOutcomePatValidator(IPointAccountTypeCache? cache = null) =>
        new(cache ?? new PermissivePatCache());

    public static CampaignDefinitionValidator CreateDefinitionValidator(
        IModelAdapter? modelAdapter = null,
        IPointAccountTypeCache? patCache = null) =>
        new(CreateTaxonomyValidator(modelAdapter), CreatePointOutcomePatValidator(patCache));

    public static CampaignValidationOrchestrator CreateValidationOrchestrator(
        IModelAdapter? modelAdapter = null,
        IPointAccountTypeCache? patCache = null) =>
        new(CreateTaxonomyValidator(modelAdapter), CreatePointOutcomePatValidator(patCache));

    public static void AddRootEntryNavigation(JourneyNode journey)
    {
        journey.NavigationCriteria ??= new Dictionary<NavigationType, INavigationCriteria>();
        journey.NavigationCriteria[NavigationType.Entry] = new SimpleNavigationCriteria
        {
            NavConstraint = new SimpleRule<bool>(
                new ConstantValueProvider(true),
                new ConstantValueProvider(true),
                new BoolEvaluation()),
            NavigationType = NavigationType.Entry,
            Outcomes = new List<OutcomeBase>()
        };
    }

    internal sealed class PermissivePatCache : IPointAccountTypeCache
    {
        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => true;

        public Task<bool> EnsurePATsLoaded(string tenantId) => Task.FromResult(true);

        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) =>
            Task.FromResult(new List<PointAccountType>());

        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            Task.FromResult(new PointAccountType(
                null,
                "Active",
                "TestPat",
                null,
                PointLedgerTypeStrings.SPENDABLE,
                null,
                null,
                null,
                true,
                "AwayFromZero",
                0,
                tenantId,
                pointAccountTypeId));

        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            Task.CompletedTask;

        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => Task.CompletedTask;
    }

    private sealed class CampaignTestStubModelAdapter : IModelAdapter
    {
        public Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null) =>
            throw new NotImplementedException();

        public Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelDto?>(null);
    }

    private sealed class CampaignTestNopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
