using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Journeys.Tests.Services
{
    public class LoyaltyAccountExpirationCascadeTests
    {
        private const string Dest365Id = "dest-365";
        private const string DestEndDateId = "dest-end-date";
        private const string DestNeitherId = "dest-neither";
        private const string MissingDestId = "pat-not-in-cache";
        private const string ThrowingDestId = "dest-throws";

        private static readonly DateTimeOffset EarnDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset DestEndDate = new DateTimeOffset(2028, 6, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset ExistingExpiration = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task Hop_keeps_EarnDate_and_sets_dest_expiration_from_EarnDate_plus_365_days()
        {
            var ctx = CreateContext(Dest365());
            var earn = EarnDate;
            await SeedDueEscrowAsync(ctx, earn, expiration: earn.AddDays(30));

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, earn, 1.0m);

            var dest = await DestEntryAsync(ctx, Dest365Id);
            Assert.Equal(earn, dest.EarnDate);
            Assert.Equal(earn.AddDays(365), dest.ExpirationDate);
            Assert.Null(await SourceEntryAsync(ctx, ctx.Escrow.Id));
        }

        [Fact]
        public async Task Hop_uses_dest_end_date_when_set()
        {
            var destPat = Dest365();
            destPat = new PointAccountType(
                destPat.ExtAccountId, destPat.Status, destPat.Name, destPat.PointSourceId, destPat.LedgerType,
                365, DestEndDate, destPat.ExpiresToPointAccountTypeId, destPat.IsSpendable,
                destPat.RoundingOptionString, destPat.RoundingDecimalPlaces, destPat.TenantId, DestEndDateId);
            var ctx = CreateContext(destPat);
            await SeedDueEscrowAsync(ctx, EarnDate, EarnDate.AddDays(30));

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m);

            var dest = await DestEntryAsync(ctx, DestEndDateId);
            Assert.Equal(EarnDate, dest.EarnDate);
            Assert.Equal(DestEndDate, dest.ExpirationDate);
        }

        [Fact]
        public async Task Hop_unsets_dest_expiration_when_dest_has_neither_clock()
        {
            var destPat = new PointAccountType(
                null, "Active", "Dest neither", "Purchase", "SPENDABLE",
                null, null, null, true, "AwayFromZero", 0, TestDataFactory.TENANT_ID, DestNeitherId);
            var ctx = CreateContext(destPat);
            await SeedDueEscrowAsync(ctx, EarnDate, EarnDate.AddDays(30));

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m);

            var dest = await DestEntryAsync(ctx, DestNeitherId);
            Assert.Equal(EarnDate, dest.EarnDate);
            Assert.True(dest.ExpirationDate == null || dest.ExpirationDate.Value.Year <= 1);
        }

        [Fact]
        public async Task Missing_dest_PAT_leaves_row_in_source()
        {
            var escrow = TestDataFactory.GetEscrowPointAccount();
            escrow.ExpiresToPointAccountTypeId = MissingDestId;
            var ctx = CreateContext(Dest365(), escrow);
            await SeedDueEscrowAsync(ctx, EarnDate, EarnDate.AddDays(30));

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m);

            Assert.NotNull(await SourceEntryAsync(ctx, ctx.Escrow.Id));
            var points = await ctx.Service.GetLoyaltyAccountPointsAsync(ctx.TenantId, ctx.AccountId);
            Assert.DoesNotContain(points, l => l.PointAccountTypeId == MissingDestId);
        }

        [Fact]
        public async Task Dest_PAT_load_throw_propagates()
        {
            var destPat = Dest365();
            destPat = new PointAccountType(
                destPat.ExtAccountId, destPat.Status, destPat.Name, destPat.PointSourceId, destPat.LedgerType,
                365, null, destPat.ExpiresToPointAccountTypeId, destPat.IsSpendable,
                destPat.RoundingOptionString, destPat.RoundingDecimalPlaces, destPat.TenantId, ThrowingDestId);
            var ctx = CreateContext(destPat, throwOnDestId: ThrowingDestId);
            await SeedDueEscrowAsync(ctx, EarnDate, EarnDate.AddDays(30));

            await Assert.ThrowsAnyAsync<Exception>(() =>
                ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                    ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m));
        }

        [Fact]
        public async Task Missing_EarnDate_uses_existing_expiration_when_dest_has_days()
        {
            var ctx = CreateContext(Dest365());
            await SeedDueEscrowAsync(ctx, EarnDate, ExistingExpiration);
            ClearEarnDateOnSource(ctx);

            await ctx.Service.ExpireLoyaltyAccountPointsByExpirationDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, ExistingExpiration, 1.0m);

            var dest = await DestEntryAsync(ctx, Dest365Id);
            Assert.True(dest.EarnDate == null || dest.EarnDate.Value.Year <= 1);
            Assert.Equal(ExistingExpiration, dest.ExpirationDate);
        }

        [Fact]
        public async Task Already_cascaded_UtcNow_dest_date_stays_when_hop_does_not_run()
        {
            var ctx = CreateContext(Dest365());
            var cascaded = DateTimeOffset.UtcNow.AddDays(10);
            await ctx.Service.UpsertLoyaltyAccountAsync(ctx.TenantId, ctx.Account.ToDto());
            await ctx.Service.UpsertLoyaltyAccountPointsAsync(ctx.TenantId, new PointLedgerDto
            {
                TenantId = ctx.TenantId,
                AccountId = ctx.AccountId,
                PointAccountTypeId = Dest365Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "already-hopped",
                        EventType = "evtType",
                        EarnDate = EarnDate,
                        ExpirationDate = cascaded,
                        PointsDeposited = 25,
                        SpendablePoints = 25
                    }
                }
            }, ctx.Account);

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m);

            var dest = await DestEntryAsync(ctx, Dest365Id);
            Assert.Equal(cascaded, dest.ExpirationDate);
            Assert.Equal(EarnDate, dest.EarnDate);
        }

        [Fact]
        public async Task Dest_end_date_wins_over_dest_days()
        {
            var destPat = new PointAccountType(
                null, "Active", "Dest end wins", "Purchase", "SPENDABLE",
                365, DestEndDate, null, true, "AwayFromZero", 0, TestDataFactory.TENANT_ID, DestEndDateId);
            var ctx = CreateContext(destPat);
            await SeedDueEscrowAsync(ctx, EarnDate, EarnDate.AddDays(30));

            await ctx.Service.ExpireLoyaltyAccountPointsByEarnDate(
                ctx.TenantId, ctx.AccountId, new List<string> { ctx.Escrow.Id }, EarnDate, 1.0m);

            var dest = await DestEntryAsync(ctx, DestEndDateId);
            Assert.Equal(DestEndDate, dest.ExpirationDate);
        }

        private static PointAccountType Dest365()
        {
            return new PointAccountType(
                null, "Active", "Dest 365", "Purchase", "SPENDABLE",
                365, null, null, true, "AwayFromZero", 0, TestDataFactory.TENANT_ID, Dest365Id);
        }

        private static CascadeContext CreateContext(PointAccountType dest, PointAccountType escrow = null, string throwOnDestId = null)
        {
            var cache = throwOnDestId == null
                ? new StubPointAccountTypeCache()
                : new ThrowingDestCache(throwOnDestId);
            escrow ??= TestDataFactory.GetEscrowPointAccount();
            if (string.IsNullOrEmpty(escrow.ExpiresToPointAccountTypeId))
                escrow.ExpiresToPointAccountTypeId = dest.Id;
            cache.CachePointAccountType(escrow.TenantId, escrow);
            if (throwOnDestId == null)
                cache.CachePointAccountType(dest.TenantId, dest);

            var account = TestDataFactory.GenerateRichard();
            var ledgerAdapter = new StubPointLedgerAdapter();
            var service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                ledgerAdapter,
                new StubTagAdapter(),
                cache,
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(),
                default(IDynamicDataAdapter),
                default(IDynamicExternalReferenceAdapter),
                default(IDataLakeAdapter));

            return new CascadeContext
            {
                Service = service,
                Cache = cache,
                LedgerAdapter = ledgerAdapter,
                Escrow = escrow,
                Dest = dest,
                Account = account,
                TenantId = account.TenantId,
                AccountId = account.Id
            };
        }

        private static async Task SeedDueEscrowAsync(CascadeContext ctx, DateTimeOffset earnDate, DateTimeOffset expiration)
        {
            await ctx.Service.UpsertLoyaltyAccountAsync(ctx.TenantId, ctx.Account.ToDto());
            await ctx.Service.UpsertLoyaltyAccountPointsAsync(ctx.TenantId, new PointLedgerDto
            {
                TenantId = ctx.TenantId,
                AccountId = ctx.AccountId,
                PointAccountTypeId = ctx.Escrow.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "escrow-hop",
                        EventType = "evtType",
                        EarnDate = earnDate,
                        ExpirationDate = expiration,
                        PointsDeposited = 100,
                        SpendablePoints = 100
                    }
                }
            }, ctx.Account);
        }

        private static void ClearEarnDateOnSource(CascadeContext ctx)
        {
            var page = ctx.LedgerAdapter.FetchLoyaltyAccountLedgersAsync(ctx.TenantId, ctx.AccountId, 100).GetAwaiter().GetResult();
            var source = page.Entities.First(l => l.PointAccountTypeId == ctx.Escrow.Id);
            foreach (var list in source.Entries.Values)
            {
                foreach (var entry in list)
                    entry.EarnDate = null;
            }
        }

        private static async Task<LedgerEntryDto> DestEntryAsync(CascadeContext ctx, string destPatId)
        {
            var points = await ctx.Service.GetLoyaltyAccountPointsAsync(ctx.TenantId, ctx.AccountId);
            var dest = points.FirstOrDefault(l => l.PointAccountTypeId == destPatId);
            Assert.NotNull(dest);
            var entry = dest.LedgerEntries?.FirstOrDefault();
            Assert.NotNull(entry);
            return entry;
        }

        private static async Task<LedgerEntryDto> SourceEntryAsync(CascadeContext ctx, string sourcePatId)
        {
            var points = await ctx.Service.GetLoyaltyAccountPointsAsync(ctx.TenantId, ctx.AccountId);
            var source = points.FirstOrDefault(l => l.PointAccountTypeId == sourcePatId);
            return source?.LedgerEntries?.FirstOrDefault();
        }

        private sealed class CascadeContext
        {
            public LoyaltyAccountService Service { get; set; }
            public StubPointAccountTypeCache Cache { get; set; }
            public StubPointLedgerAdapter LedgerAdapter { get; set; }
            public PointAccountType Escrow { get; set; }
            public PointAccountType Dest { get; set; }
            public LoyaltyAccount Account { get; set; }
            public string TenantId { get; set; }
            public string AccountId { get; set; }
        }

        private sealed class ThrowingDestCache : StubPointAccountTypeCache
        {
            private readonly string _throwId;

            public ThrowingDestCache(string throwId)
            {
                _throwId = throwId;
            }

            public override Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId)
            {
                if (pointAccountTypeId == _throwId)
                    throw new InvalidOperationException("dest-PAT infra");
                return base.GetPointAccountTypeAsync(tenantId, pointAccountTypeId);
            }
        }
    }
}
