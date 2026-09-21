using Journeys.Core.Caching;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.Tests.Stubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Journeys.Tests.RulesEngine.Outcomes;

public class NotificationOutcomeTests
{
    private const string ConfigId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OutcomeId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string CampaignId = "campaign-notify";
    private const string RuleSetId = "ruleset-notify";
    private const string EventModelId = "Order";

    private static readonly string[] ClosedPayloadPropertyNames =
    [
        nameof(NotificationOutcomePayload.TenantId),
        nameof(NotificationOutcomePayload.LoyaltyAccountId),
        nameof(NotificationOutcomePayload.ExtAccountId),
        nameof(NotificationOutcomePayload.CampaignId),
        nameof(NotificationOutcomePayload.RuleSetId),
        nameof(NotificationOutcomePayload.IssuingOutcomeId),
        nameof(NotificationOutcomePayload.IssuingOutcomeKind),
        nameof(NotificationOutcomePayload.EventId),
        nameof(NotificationOutcomePayload.EventType),
        nameof(NotificationOutcomePayload.EventModelId),
        nameof(NotificationOutcomePayload.AwardedAtUtc)
    ];

    [Fact]
    public async Task Happy_Award_posts_closed_payload_for_this_tenant_and_config_only()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        var fake = CreateFakeWithActiveConfig(state);
        state.NotificationService = fake;
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();

        // Act
        var calculated = await outcome.CalculateOutcomeAsync(state, default);
        Assert.NotNull(calculated);
        Assert.False(calculated.IsAwarded);
        var awarded = await outcome.AwardOutcomeAsync(state, loyalty, default);

        // Assert
        Assert.NotNull(awarded);
        Assert.True(awarded.IsAwarded);
        var send = Assert.Single(fake.Sends);
        Assert.Equal(state.TenantId, send.TenantId);
        Assert.Equal(ConfigId, send.ConfigId);
        Assert.IsType<NotificationOutcomePayload>(send.Payload);
        var payload = (NotificationOutcomePayload)send.Payload;
        Assert.Equal(state.TenantId, payload.TenantId);
        Assert.Equal(state.LoyaltyAccountId, payload.LoyaltyAccountId);
        Assert.Equal(state.LoyaltyAccount.ExtAccountId, payload.ExtAccountId);
        Assert.Equal(CampaignId, payload.CampaignId);
        Assert.Equal(RuleSetId, payload.RuleSetId);
        Assert.Equal(OutcomeId, payload.IssuingOutcomeId);
        Assert.Equal(OutcomeKindDiscriminators.NotificationOutcome, payload.IssuingOutcomeKind);
        Assert.Equal(state.EventId, payload.EventId);
        Assert.Equal(state.EventType, payload.EventType);
        Assert.Equal(EventModelId, payload.EventModelId);
        Assert.True(payload.AwardedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.True(payload.AwardedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(5));
        var actualNames = send.Payload.GetType().GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ClosedPayloadPropertyNames.OrderBy(n => n).ToArray(), actualNames);
        Assert.DoesNotContain(fake.GetConfigCalls, c => c.TenantId != state.TenantId);
    }

    [Fact]
    public async Task Missing_NotificationConfigId_Calculate_returns_null()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        state.NotificationService = CreateFakeWithActiveConfig(state);
        var outcome = CreateOutcome();
        outcome.NotificationConfigId = null;

        // Act
        var result = await outcome.CalculateOutcomeAsync(state, default);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Missing_inactive_mismatched_or_foreign_tenant_config_Calculate_returns_null()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        var outcome = CreateOutcome();

        var missingFake = new FakeNotificationService();
        state.NotificationService = missingFake;

        // Act / Assert — missing config for this tenant
        Assert.Null(await outcome.CalculateOutcomeAsync(state, default));

        var inactive = CreateConfig(state.TenantId, EventModelId, ConfigStatus.INACTIVE);
        var inactiveFake = new FakeNotificationService();
        inactiveFake.Configs[(state.TenantId, ConfigId)] = inactive;
        state.NotificationService = inactiveFake;
        Assert.Null(await outcome.CalculateOutcomeAsync(state, default));

        var mismatched = CreateConfig(state.TenantId, "OtherEventModel", ConfigStatus.ACTIVE);
        var mismatchFake = new FakeNotificationService();
        mismatchFake.Configs[(state.TenantId, ConfigId)] = mismatched;
        state.NotificationService = mismatchFake;
        Assert.Null(await outcome.CalculateOutcomeAsync(state, default));

        var foreign = CreateConfig("foreign-tenant", EventModelId, ConfigStatus.ACTIVE);
        var foreignFake = new FakeNotificationService();
        foreignFake.Configs[("foreign-tenant", ConfigId)] = foreign;
        state.NotificationService = foreignFake;
        Assert.Null(await outcome.CalculateOutcomeAsync(state, default));
        Assert.All(foreignFake.GetConfigCalls, c => Assert.Equal(state.TenantId, c.TenantId));
    }

    [Fact]
    public async Task Send_false_Award_IsAwarded_false_and_sibling_awards_are_kept()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        state.ProcessingJourney = new JourneyNode("root", id: "root", rootNodeId: "root");
        var fake = CreateFakeWithActiveConfig(state);
        fake.SendResult = false;
        state.NotificationService = fake;
        var sibling = new OutcomeResult
        {
            IssuingOutcomeId = "sibling-tag",
            IssuingOutcomeKind = OutcomeKindDiscriminators.TagOutcome,
            IsAwarded = true
        };
        state.TryAddUpdateEarnedOutcomes(sibling);
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();

        // Act
        Assert.NotNull(await outcome.CalculateOutcomeAsync(state, default));
        var awarded = await outcome.AwardOutcomeAsync(state, loyalty, default);

        // Assert
        Assert.NotNull(awarded);
        Assert.False(awarded.IsAwarded);
        Assert.Single(fake.Sends);
        Assert.Contains(
            state.EarnedOutcomes.Values.SelectMany(x => x),
            x => x.IssuingOutcomeId == "sibling-tag" && x.IsAwarded);
    }

    [Fact]
    public async Task Send_throw_default_fail_closed_propagates_exception()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        var fake = CreateFakeWithActiveConfig(state);
        fake.SendThrow = new InvalidOperationException("webhook down");
        state.NotificationService = fake;
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();
        Assert.NotNull(await outcome.CalculateOutcomeAsync(state, default));

        // Act / Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => outcome.AwardOutcomeAsync(state, loyalty, default));
        Assert.Equal("webhook down", ex.Message);
    }

    [Fact]
    public async Task Send_throw_with_TreatNotificationSendThrowAsFalse_is_same_as_send_false()
    {
        // Arrange
        var state = CreateState(calculateOnly: false);
        state.TreatNotificationSendThrowAsFalse = true;
        var fake = CreateFakeWithActiveConfig(state);
        fake.SendThrow = new InvalidOperationException("webhook down");
        state.NotificationService = fake;
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();

        // Act
        Assert.NotNull(await outcome.CalculateOutcomeAsync(state, default));
        var awarded = await outcome.AwardOutcomeAsync(state, loyalty, default);

        // Assert
        Assert.NotNull(awarded);
        Assert.False(awarded.IsAwarded);
        Assert.Empty(fake.Sends);
    }

    [Fact]
    public async Task CalculateOnly_may_resolve_but_Award_does_not_send()
    {
        // Arrange
        var state = CreateState(calculateOnly: true);
        var fake = CreateFakeWithActiveConfig(state);
        state.NotificationService = fake;
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();

        // Act
        var calculated = await outcome.CalculateOutcomeAsync(state, default);
        var awarded = await outcome.AwardOutcomeAsync(state, loyalty, default);

        // Assert
        Assert.NotNull(calculated);
        Assert.False(calculated.IsAwarded);
        Assert.Empty(fake.Sends);
        if (awarded != null)
            Assert.False(awarded.IsAwarded);
    }

    [Fact]
    public async Task Award_IsAwarded_false_is_honored_by_award_loop_and_signature_stays()
    {
        // Arrange
        var awardMethod = typeof(NotificationOutcome).GetMethod(nameof(NotificationOutcome.AwardOutcomeAsync));
        Assert.NotNull(awardMethod);
        var parameters = awardMethod!.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(RulesEngineState), parameters[0].ParameterType);
        Assert.Equal(typeof(ILoyaltyAccountService), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);

        var state = CreateState(calculateOnly: false);
        var fake = CreateFakeWithActiveConfig(state);
        fake.SendResult = false;
        state.NotificationService = fake;
        var outcome = CreateOutcome();
        var loyalty = TestDataFactory.CreateService();

        // Act
        Assert.NotNull(await outcome.CalculateOutcomeAsync(state, default));
        var awarded = await outcome.AwardOutcomeAsync(state, loyalty, default);

        var campaign = CreateLiveCampaign(outcome);
        var account = TestDataFactory.GenerateRichard();
        var request = new RulesServiceRequest(
            payloadModelId: EventModelId,
            payload: TestDataFactory.GenerateOrder1(),
            u: account,
            campaigns: new List<Campaign> { campaign },
            globals: TestDataFactory.GenerateGlobals(),
            calculateOnly: false,
            eventId: "evt-loop",
            eventType: "Order")
        {
            NotificationService = fake
        };
        var processResult = await CreateRulesService().ProcessRulesAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(awarded);
        Assert.False(awarded.IsAwarded);
        var notificationResults = processResult.State.EarnedOutcomes.Values
            .SelectMany(x => x)
            .Where(x => x.IssuingOutcomeKind == OutcomeKindDiscriminators.NotificationOutcome)
            .ToList();
        Assert.NotEmpty(notificationResults);
        Assert.All(notificationResults, r => Assert.False(r.IsAwarded));
    }

    [Fact]
    public void AddNotificationsServices_registers_INotificationService_when_DisableDataLake_is_true()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DisableDataLake"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        // Act
        services.AddNotificationsServices(config);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(INotificationService));
    }

    private static RulesEngineState CreateState(bool calculateOnly)
    {
        var stubCache = new StubPointAccountTypeCache();
        stubCache.RegisterTestCampaignFactoryPointAccountTypes();
        var state = TestDataFactory.GetProfileObject();
        state.CalculateOnly = calculateOnly;
        state.EventModelId = EventModelId;
        state.CurrentCampaignId = CampaignId;
        state.AppliedRuleSets.Add(RuleSetId);
        return state;
    }

    private static NotificationOutcome CreateOutcome()
    {
        return new NotificationOutcome
        {
            Id = OutcomeId,
            NotificationConfigId = ConfigId,
            EventTypeProvider = null
        };
    }

    private static FakeNotificationService CreateFakeWithActiveConfig(RulesEngineState state)
    {
        var fake = new FakeNotificationService();
        fake.Configs[(state.TenantId, ConfigId)] = CreateConfig(state.TenantId, EventModelId, ConfigStatus.ACTIVE);
        return fake;
    }

    private static NotificationConfig CreateConfig(string tenantId, string? eventModelId, string status)
    {
        return new NotificationConfig(
            tenantId,
            ConfigId,
            "rest webhook",
            AdapterTypes.REST_API,
            status,
            JsonSerializer.SerializeToElement(new { }),
            eventModelId);
    }

    private static Campaign CreateLiveCampaign(NotificationOutcome outcome)
    {
        var rootId = "notify-root";
        var ruleSet = new RuleSet
        {
            Name = RuleSetId,
            RuleTree = new SimpleRule<bool>(
                new ConstantValueProvider(true),
                new ConstantValueProvider(true),
                new BoolEvaluation()),
            Outcomes = new List<OutcomeBase> { outcome }
        };
        var root = new JourneyNode(rootId, rules: new List<RuleSet> { ruleSet }, id: rootId, rootNodeId: rootId);
        root.NavigationCriteria = new Dictionary<NavigationType, INavigationCriteria>
        {
            [NavigationType.Entry] = new SimpleNavigationCriteria(
                "Entry",
                NavigationType.Entry,
                new SimpleRule<bool>(
                    new ConstantValueProvider(true),
                    new ConstantValueProvider(true),
                    new BoolEvaluation()),
                null)
        };
        return new Campaign(
            extCampaignId: CampaignId,
            status: CampaignStatusStrings.Live,
            name: CampaignId,
            events: new List<string> { "OrderEvent" },
            startDate: DateTimeOffset.UtcNow.AddDays(-30),
            endDate: null,
            segments: null,
            journey: root,
            tenantId: TestDataFactory.TENANT_ID,
            id: CampaignId);
    }

    private static RulesService CreateRulesService()
    {
        var stubCache = new StubPointAccountTypeCache();
        stubCache.RegisterTestCampaignFactoryPointAccountTypes();
        return new RulesService(
            new StubUserJourneyAdapter(),
            null,
            new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                stubCache,
                new StubLoyaltyAccountPointsDetailsAdapter(),
                NullLogger<LoyaltyAccountService>.Instance,
                default(IDynamicDataAdapter),
                default(IDynamicExternalReferenceAdapter),
                default(IDataLakeAdapter)),
            NullLogger<RulesService>.Instance,
            default(ITaxonomyDataAdapter),
            default(IDynamicDataAdapter),
            default(ModelCache),
            new StubHistoricalRuleStateTTLAdapter());
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Dictionary<(string TenantId, string ConfigId), NotificationConfig?> Configs { get; } = new();
        public List<(string TenantId, string ConfigId)> GetConfigCalls { get; } = new();
        public List<(string TenantId, string ConfigId, object Payload)> Sends { get; } = new();
        public bool SendResult { get; set; } = true;
        public Exception? SendThrow { get; set; }

        public Task<NotificationConfig?> GetNotificationConfigAsync(string tenantId, string configId)
        {
            GetConfigCalls.Add((tenantId, configId));
            Configs.TryGetValue((tenantId, configId), out var config);
            return Task.FromResult(config);
        }

        public Task<bool> SendNotificationAsync(string tenantId, string configId, object payload)
        {
            if (SendThrow != null)
                throw SendThrow;
            Sends.Add((tenantId, configId, payload));
            return Task.FromResult(SendResult);
        }

        public Task<NotificationConfig> UpsertNotificationConfigAsync(string tenantId, NotificationConfigDto config)
            => throw new NotImplementedException();

        public Task<IEnumerable<NotificationConfig>> GetNotificationConfigsAsync(string tenantId, string status)
            => throw new NotImplementedException();

        public Task<bool> DeleteNotificationConfigAsync(string tenantId, string configId)
            => throw new NotImplementedException();

        public Task<bool> SendNotificationsAsync(string tenantId, ProcessedEventDto processedEventDto)
            => throw new NotImplementedException();

        public Task<bool> SendNotificationsAsync(string tenantId, PointLedgerDto ledgerDto)
            => throw new NotImplementedException();

        public Task<bool> SendNotificationAsync(string tenantId, NotificationConfig config, object payload)
            => throw new NotImplementedException();
    }
}
