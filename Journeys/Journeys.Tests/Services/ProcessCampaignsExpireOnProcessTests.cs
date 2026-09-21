using Journeys.Core.Exceptions;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Tests.Services
{
    public class ProcessCampaignsExpireOnProcessTests
    {
        private const string TenantId = TestDataFactory.TENANT_ID;
        private const string AccountId = "acct-expire-on-process";
        private const string BringCurrentName = "BringLoyaltyAccountPointsCurrentInternalAsync";

        [Fact]
        public async Task After_lock_bring_current_runs_before_rules_and_assigns_released_PointLedgers()
        {
            var ctx = CreateContext();

            await ctx.EventService.ProcessCampaignsAsync(
                TenantId, "model-1", EmptyPayload(), ctx.Account, EmptyCampaigns(), false, "evt-1", "order");

            Assert.Equal(new[] { "lock", "bring-current", "rules" }, ctx.CallOrder);
            Assert.Same(ctx.ReleasedLedgers, ctx.Rules.LastRequest!.LoyaltyAccount.PointLedgers);
            Assert.Contains(ctx.Rules.LastRequest.LoyaltyAccount.PointLedgers, l => l.Id == "released-hold");
        }

        [Fact]
        public async Task Call_order_is_lock_then_bring_current_then_notification_port_then_rules()
        {
            var ctx = CreateContext();

            await ctx.EventService.ProcessCampaignsAsync(
                TenantId, "model-1", EmptyPayload(), ctx.Account, EmptyCampaigns(), false, "evt-1", "order");

            Assert.Equal(new[] { "lock", "bring-current", "rules" }, ctx.CallOrder);
            Assert.Same(ctx.Notifications, ctx.Rules.LastRequest!.NotificationService);
        }

        [Fact]
        public async Task Bring_current_throw_after_lock_fails_event_and_skips_rules()
        {
            var ctx = CreateContext();
            ctx.Loyalty.BringCurrentThrow = new InvalidOperationException("expire failed");

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ctx.EventService.ProcessCampaignsAsync(
                    TenantId, "model-1", EmptyPayload(), ctx.Account, EmptyCampaigns(), false, "evt-1", "order"));

            Assert.Equal("expire failed", thrown.Message);
            Assert.Equal(new[] { "lock", "bring-current" }, ctx.CallOrder);
            Assert.Null(ctx.Rules.LastRequest);
        }

        [Fact]
        public async Task Lock_not_acquired_skips_bring_current_and_keeps_existing_lease()
        {
            var ctx = CreateContext();
            ctx.Loyalty.AcquireLock = false;

            await Assert.ThrowsAsync<Exception>(() =>
                ctx.EventService.ProcessCampaignsAsync(
                    TenantId, "model-1", EmptyPayload(), ctx.Account, EmptyCampaigns(), false, "evt-1", "order"));

            Assert.DoesNotContain("bring-current", ctx.CallOrder);
            Assert.DoesNotContain("rules", ctx.CallOrder);
            Assert.Equal(3, ctx.Loyalty.LockCalls.Count);
            Assert.All(ctx.Loyalty.LockCalls, c =>
            {
                Assert.Equal(TenantId, c.TenantId);
                Assert.Equal(500, c.LeaseMs);
                Assert.False(string.IsNullOrWhiteSpace(c.LockKey));
                Assert.True(Guid.TryParse(c.LockKey, out _));
            });
        }

        [Fact]
        public void ProcessEventInternalAsync_has_no_bring_current_call_site_only_ProcessCampaignsAsync()
        {
            var source = EventServiceSource();
            var processEvent = MethodBody(source, "ProcessEventInternalAsync(");
            var processCampaigns = MethodBody(source, "internal async Task<RulesServiceResponse> ProcessCampaignsAsync(");

            Assert.DoesNotContain(BringCurrentName, processEvent);
            Assert.Contains(BringCurrentName, processCampaigns);
        }

        [Fact]
        public void Get_reconcile_and_resettle_call_sites_are_not_rewritten()
        {
            var source = EventServiceSource();
            var byFile = MethodBody(source, "ReconcileLoyaltyAccountEventsByFileAsync(");
            var reconcile = MethodBody(source, "public async Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsAsync(");
            var resettle = MethodBody(source, "ResettleAccountInternalAsync(");

            Assert.Contains("PointLedgers ??= await _loyaltyAccountService." + BringCurrentName, byFile);
            Assert.Contains("PointLedgers ??= await _loyaltyAccountService." + BringCurrentName, reconcile);
            Assert.DoesNotContain(BringCurrentName, resettle);
        }

        [Fact]
        public void EventService_does_not_compute_dest_expiration()
        {
            var source = EventServiceSource();

            Assert.DoesNotContain("ComputeDestExpiration", source);
            Assert.DoesNotContain("SaveLedgerExpirations", source);
            Assert.DoesNotContain("PointsLifespanDays", source);
            Assert.DoesNotContain("PointsLifespanEndDate", source);
            Assert.DoesNotContain("/points/expire", source);
        }

        private static Harness CreateContext()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();

            var callOrder = new List<string>();
            var account = new LoyaltyAccount(
                extAccountId: AccountId,
                type: "account",
                status: "Active",
                lockLeaseKey: null,
                lockLeaseExpiration: null,
                tags: null,
                journeys: null,
                knownExternalIds: null,
                tenantId: TenantId,
                id: AccountId);

            var dueEscrow = TestDataFactory.GenerateEscrowLedger();
            foreach (var entries in dueEscrow.Entries!.Values)
            {
                foreach (var entry in entries)
                    entry.ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1);
            }
            account.PointLedgers = new List<PointLedger> { dueEscrow };

            var released = TestDataFactory.GenerateSpendableLedger();
            released = new PointLedger(
                account.Id,
                released.PointAccountTypeId,
                100,
                100,
                null,
                released.Entries,
                TenantId,
                "released-hold");
            released.PointAccountType = TestDataFactory.GetSpendablePointAccount();

            var loyalty = new FakeLoyaltyAccountService(callOrder) { ReleasedLedgers = new List<PointLedger> { released } };
            var rules = new FakeRulesService(callOrder);
            var notifications = new FakeNotificationService();
            var eventService = new EventService(
                loyalty,
                default!,
                default!,
                rules,
                default!,
                default!,
                default!,
                default!,
                LoggerFactoryProvider.CreateLogger<EventService>(),
                default!,
                default!,
                notifications);

            return new Harness(eventService, loyalty, rules, notifications, account, callOrder, loyalty.ReleasedLedgers);
        }

        private static JsonElement EmptyPayload()
        {
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }

        private static List<Campaign> EmptyCampaigns() => new();

        private static string EventServiceSource()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Journeys.Core", "Services", "EventService.cs");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }

            throw new FileNotFoundException("EventService.cs");
        }

        private static string MethodBody(string source, string signature)
        {
            var start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing {signature}");
            var brace = source.IndexOf('{', start);
            Assert.True(brace >= 0, $"Missing body for {signature}");
            var depth = 0;
            for (var i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            throw new InvalidOperationException($"Unclosed body for {signature}");
        }

        private sealed record Harness(
            EventService EventService,
            FakeLoyaltyAccountService Loyalty,
            FakeRulesService Rules,
            FakeNotificationService Notifications,
            LoyaltyAccount Account,
            List<string> CallOrder,
            List<PointLedger> ReleasedLedgers);

        private sealed class FakeLoyaltyAccountService : ILoyaltyAccountService
        {
            private readonly List<string> _callOrder;

            public FakeLoyaltyAccountService(List<string> callOrder)
            {
                _callOrder = callOrder;
            }

            public bool AcquireLock { get; set; } = true;
            public Exception? BringCurrentThrow { get; set; }
            public List<PointLedger> ReleasedLedgers { get; set; } = new();
            public List<(string TenantId, string LockKey, int LeaseMs)> LockCalls { get; } = new();

            public Task<LoyaltyAccount> TryLockAccount(string tenantId, LoyaltyAccount account, string lockKey, int lockLeaseExpirationMS)
            {
                _callOrder.Add("lock");
                LockCalls.Add((tenantId, lockKey, lockLeaseExpirationMS));
                if (!AcquireLock)
                    throw new LockAcquisitionException("Concurrency check failure", account.Id, lockKey, "held-by-other");

                account.LockLeaseKey = lockKey;
                account.LockLeaseExpiration = DateTimeOffset.UtcNow.AddMilliseconds(lockLeaseExpirationMS);
                return Task.FromResult(account);
            }

            public Task<List<PointLedger>> BringLoyaltyAccountPointsCurrentInternalAsync(string tenantId, LoyaltyAccount loyaltyAccount)
            {
                _callOrder.Add("bring-current");
                if (BringCurrentThrow != null)
                    throw BringCurrentThrow;
                return Task.FromResult(ReleasedLedgers);
            }

            public Task<LoyaltyAccountDto> GetLoyaltyAccountAsync(string tenantId, string id, bool resettleIfNeeded = false) => throw new NotImplementedException();
            public Task<LoyaltyAccountDto> GetLoyaltyAccountByExtIdAsync(string tenantId, string extId, bool throwExceptionOnDeleted = false, bool resettleIfNeeded = false) => throw new NotImplementedException();
            public Task<List<LoyaltyAccountDto>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters) => throw new NotImplementedException();
            public Task<LoyaltyAccountDto> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account) => throw new NotImplementedException();
            public Task<ExternalReferenceDto> AliasLoyaltyAccountAsync(string tenantId, AliasAccountRequest request) => throw new NotImplementedException();
            public Task<ExternalReferenceDto> SetExternalReferenceAsync(string tenantId, ExternalReferenceDto extRef) => throw new NotImplementedException();
            public Task<LoyaltyAccount> LoadLoyaltyAccountDetailsAsync(string tenantId, LoyaltyAccount acct) => throw new NotImplementedException();
            public Task<bool> EnsureAccountInValidStateAsync(string tenantId, LoyaltyAccount acct, CancellationToken token = default) => throw new NotImplementedException();
            public Task<List<PointLedgerDto>> BringLoyaltyAccountPointsCurrentAsync(string tenantId, LoyaltyAccount loyaltyAccount, bool capilatizecapitalizeType = false) => throw new NotImplementedException();
            public Task<List<PointLedgerDto>> GetLoyaltyAccountPointsAsync(string tenantId, string accountId, bool resettleIfNeeded = false, bool capilatizecapitalizeType = false) => throw new NotImplementedException();
            public Task<PointLedgerDto> BulkDepositPointsAsync(string tenantId, BulkPointDepositRequest request) => throw new NotImplementedException();
            public Task<PointLedgerDto> DepositPointsAsync(string tenantId, PointDespositRequest request) => throw new NotImplementedException();
            public Task<PointLedgerDto> WithdrawPointsAsync(string tenantId, PointWithdrawlRequest request) => throw new NotImplementedException();
            public Task<PointLedgerDto> BulkWithdrawPointsAsync(string tenantId, BulkPointWithdrawalRequest request) => throw new NotImplementedException();
            public Task<PointLedgerDto> UpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger, LoyaltyAccount account = null) => throw new NotImplementedException();
            public Task<List<PointLedgerDto>> SetDefaultPointLedgersAsync(string tenantId, LoyaltyAccount account, List<PointAccountType> types) => throw new NotImplementedException();
            public Task<PointLedgerDto> AdminUpsertLoyaltyAccountPointsAsync(string tenantId, PointLedgerDto ledger) => throw new NotImplementedException();
            public Task<PagedResultSet<TagDto>> GetLoyaltyAccountTagsAsync(string tenantId, string accountId, string type, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
            public Task<TagDto> TagLoyaltyAccountAsync(string tenantId, TagDto tag) => throw new NotImplementedException();
            public Task<PointsDetailsResponse> UpsertPointsDetails(string tenantId, UpsertPointsDetailsRequest req) => throw new NotImplementedException();
            public Task<List<PointsDetailsResponse>> BulkUpsertPointsDetails(string tenantId, BulkUpsertPointsDetailsRequest request) => throw new NotImplementedException();
            public Task<PointsDetailsResponse> GetPointsDetails(string tenantId, string loyaltyAccountId, string pointEventId, string pointEventType) => throw new NotImplementedException();
            public Task<List<PointsDetailsResponse>> GetManyPointsDetails(string tenantId, GetManyPointsDetailsRequest request, bool capitalizeType = false) => throw new NotImplementedException();
            public Task<PagedResultSetResponse<PointsDetailsDto>> GetAllPointsDetails(string tenantId, string loyaltyAccountId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
            public Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEarnDate(string tenantId, string loyaltyAccountId, List<string> ledgerIds, DateTimeOffset date, decimal amountPercent) => throw new NotImplementedException();
            public Task<List<PointLedger>> ExpireLoyaltyAccountPointsByExpirationDate(string tenantId, string loayaltyAccountId, List<string> ledgerIds, DateTimeOffset expDate, decimal amountPercent) => throw new NotImplementedException();
            public Task<List<PointLedger>> ExpireLoyaltyAccountPointsByEventId(string tenantId, string loyaltyAccountId, List<string> ledgerIds, string eventId, decimal amount, bool isPercent) => throw new NotImplementedException();
            public Task<List<PointLedger>> ExpireLoyaltyAccountPointsByAmount(string tenantId, string loyaltyAccountId, List<string> ledgerIds, decimal amount, bool isPercent) => throw new NotImplementedException();
            public Task<string?> GetFullAccountReportAsync(string tenantId, string accountId) => throw new NotImplementedException();
            public Task<string?> GetFullAccountReportAsync(string tenantId, LoyaltyAccountDto account) => throw new NotImplementedException();
            public Task DeleteLoyaltyAccountAsync(string tenantId, string accountId) => throw new NotImplementedException();
            public Task DeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account) => throw new NotImplementedException();
            public Task SoftDeleteLoyaltyAccountAsync(string tenantId, string loyaltyAccountId) => throw new NotImplementedException();
            public Task SoftDeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccountDto account) => throw new NotImplementedException();
        }

        private sealed class FakeRulesService : IRulesService
        {
            private readonly List<string> _callOrder;

            public FakeRulesService(List<string> callOrder)
            {
                _callOrder = callOrder;
            }

            public RulesServiceRequest? LastRequest { get; private set; }

            public Task<RulesServiceResponse> ProcessRulesAsync(RulesServiceRequest request, CancellationToken token)
            {
                _callOrder.Add("rules");
                LastRequest = request;
                return Task.FromResult(new RulesServiceResponse(null!));
            }

            public Task<RulesServiceResponse> ResettleAccountJourneysAsync(RulesServiceRequest request, CancellationToken token) => throw new NotImplementedException();
            public Task<LoyaltyAccountDto> ManuallyEnterTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token, bool swapAssignment = false) => throw new NotImplementedException();
            public Task<LoyaltyAccountDto> ManuallyExitTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token) => throw new NotImplementedException();
            public Task InitState(string tenantId, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 10) => throw new NotImplementedException();
            public Task<TierMovePreviewResponse> PreviewTierMoveAsync(string tenantId, MoveTierRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
            public Task<MoveTierResponse> MoveTierAsync(string tenantId, MoveTierRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        }

        private sealed class FakeNotificationService : INotificationService
        {
            public Task<NotificationConfig> UpsertNotificationConfigAsync(string tenantId, NotificationConfigDto config) => throw new NotImplementedException();
            public Task<NotificationConfig?> GetNotificationConfigAsync(string tenantId, string configId) => throw new NotImplementedException();
            public Task<IEnumerable<NotificationConfig>> GetNotificationConfigsAsync(string tenantId, string status) => throw new NotImplementedException();
            public Task<bool> DeleteNotificationConfigAsync(string tenantId, string configId) => throw new NotImplementedException();
            public Task<bool> SendNotificationsAsync(string tenantId, ProcessedEventDto processedEventDto) => throw new NotImplementedException();
            public Task<bool> SendNotificationsAsync(string tenantId, PointLedgerDto ledgerDto) => throw new NotImplementedException();
            public Task<bool> SendNotificationAsync(string tenantId, string configId, object payload) => throw new NotImplementedException();
            public Task<bool> SendNotificationAsync(string tenantId, NotificationConfig config, object payload) => throw new NotImplementedException();
        }
    }
}
