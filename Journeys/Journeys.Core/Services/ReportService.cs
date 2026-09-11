

using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Models.Reports;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Journeys.Core.Services
{
    public class ReportService : IReportService
    {
        private readonly ILogger<ReportService> _logger;
        private readonly IReportDataAdapter _reportDataAdapter;
        private static readonly Dictionary<string, string> POINT_TYPE_MAP = new()
        {
            ["a246f62f-a9d8-4fe0-bd65-9e9d7f5881a5"] = "dealer_spendable",
            ["9c61de1a-460d-4172-8781-acc04c783880"] = "dealer_tier_qualification",
            ["36666c30-e6ca-4124-8e21-55c15be2c4bc"] = "dealer_expired",
            ["fd3526e7-8d36-4c2d-8a1c-4a87f59e8f35"] = "dealer_archive"
        };

        public ReportService(ILogger<ReportService> logger, IReportDataAdapter reportDataAdapter)
        {
            _logger = logger;
            _reportDataAdapter = reportDataAdapter;
        }

        // =========================
        //  DAILY DEALER REPORT
        // =========================
        public async Task<ReportResult> ProcessDailyReport(string tenantId)
        {
            try
            {

                var allCustomers = new List<object>();
                var allNonOrderEvents = new List<object>();

                string continuationToken = null;
                int pageSize = 100;
                int count = 0;
                do
                {

                    var stopwatch = Stopwatch.StartNew();
                    //  Fetch next batch of 10 accounts
                    var accounts = await _reportDataAdapter
                        .GetAccountsAsync(tenantId, pageSize, continuationToken);
                    count += accounts.Entities.Count();
                    continuationToken = accounts.ContinuationToken;

                    if (!accounts.Entities.Any())
                        break;

                    var accountIds = accounts.Entities.Select(x => x.Id).ToList();

                    // Fetch related data ONLY for these 10 accounts
                    var ordersTask = _reportDataAdapter.GetOrdersAsync(tenantId, accountIds, pageSize);
                    var detailsTask = _reportDataAdapter.GetDetailedAccountsAsync(tenantId, accountIds, pageSize);
                    var pointsTask = _reportDataAdapter.GetPointLedgersAsync(tenantId, accountIds, pageSize);

                    await Task.WhenAll(ordersTask, detailsTask, pointsTask);

                    var nonOrder = await GetNonOrderEntries(pointsTask.Result);
                    var spend = await GetTotalSpendData(ordersTask.Result);
                    var ytd = await GetYtdSalesData(ordersTask.Result);

                    // Build reports for THIS PAGE
                    var customerReport = BuildCustomerReport(
                        accounts.Entities,
                        detailsTask.Result,
                        pointsTask.Result,
                        spend,
                        nonOrder,
                        ytd
                    );

                    var nonOrderReport = BuildNonOrderReport(
                        nonOrder,
                        detailsTask.Result,
                        accounts.Entities
                    );

                    // Accumulate
                    allCustomers.AddRange((IEnumerable<object>)customerReport.Customers);
                    allNonOrderEvents.AddRange((IEnumerable<object>)nonOrderReport.Events);
                    stopwatch.Stop();
                    Debug.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")}: processed {count} records in ({stopwatch.Elapsed.TotalSeconds} sec)");

                } while (!string.IsNullOrEmpty(continuationToken));

                // Final output
                return new ReportResult
                {
                    CurrentDateTime = DateTime.UtcNow.ToString("O"),
                    SnapshotDateEndOfDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
                    customersReportJson = allCustomers,
                    nonOrderReportJson = allNonOrderEvents
                };
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // =========================
        // AGGREGATION Data for dealer report 
        // =========================
        private CustomerReportResult BuildCustomerReport(
        List<LoyaltyAccount> basic,
        List<LoyaltyAccountRuleState> detailed,
        List<PointLedger> points,
        List<TotalSpendDto> totalSpend,
        List<PointLedgerDto> nonOrderEntries,
        List<YtdSalesDto> ytdSales)
        {
            var detailedById = detailed.ToDictionary(x => x.LoyaltyAccountId);
            var ytdById = ytdSales.ToDictionary(x => x.LoyaltyAccountId);
            var redemptionTotals = CalculateRedemptions(nonOrderEntries);

            var result = new CustomerReportResult
            {
                CurrentDateTime = DateTime.UtcNow.ToString("O"),
                SnapshotDateEndOfDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
            };

            foreach (var b in basic.Where(x => detailedById.ContainsKey(x.Id)))
            {
                var id = b.Id;
                var detail = detailedById[id];
                var pointData = BuildPointData(points, id);

                result.Customers.Add(new CustomerReport
                {
                    CustomerId = b.ExtAccountId,
                    CustomerName = detail.Event.DealerCompanyName,
                    IsActive = !detail.Event.IsDisabled,

                    PointsRedeemedYtd = redemptionTotals.GetValueOrDefault(id, 0),
                    Tier = b.Journeys?.FirstOrDefault()?.JourneyNodeIds?.FirstOrDefault() ?? "Not In Tier",

                    YtdSalesDollars = ytdById.GetValueOrDefault(id)?.YtdSalesDollars ?? 0,
                    YtdSalesCount = ytdById.GetValueOrDefault(id)?.YtdSalesCount ?? 0,

                    DealerArchiveCurrentBalance = pointData.GetValueOrDefault("dealer_archive_currentBalance"),
                    DealerArchiveLifetimeTotal = pointData.GetValueOrDefault("dealer_archive_lifetimeTotal"),

                    DealerSpendableCurrentBalance = pointData.GetValueOrDefault("dealer_spendable_currentBalance"),
                    DealerSpendableLifetimeTotal = pointData.GetValueOrDefault("dealer_spendable_lifetimeTotal"),

                    DealerExpiredCurrentBalance = pointData.GetValueOrDefault("dealer_expired_currentBalance"),
                    DealerExpiredLifetimeTotal = pointData.GetValueOrDefault("dealer_expired_lifetimeTotal"),

                    DealerTierQualifyingCurrentBalance = pointData.GetValueOrDefault("dealer_tier_qualification_currentBalance"),
                    DealerTierQualifyingLifetimeTotal = pointData.GetValueOrDefault("dealer_tier_qualification_lifetimeTotal"),

                    LastModifiedDate = DateTime.UtcNow.ToString("O")
                });
            }

            return result;
        }

        private NonOrderReportResult BuildNonOrderReport(
            List<PointLedgerDto> nonOrderEntries,
            List<LoyaltyAccountRuleState> detailed,
            List<LoyaltyAccount> basic)
        {
            var detailedBy = detailed.ToDictionary(x => x.LoyaltyAccountId);
            var basicBy = basic.ToDictionary(x => x.Id);

            var result = new NonOrderReportResult
            {
                CurrentDateTime = DateTime.UtcNow.ToString("O"),
                SnapshotDateEndOfDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")
            };

            foreach (var acc in nonOrderEntries)
            {
                detailedBy.TryGetValue(acc.AccountId, out var detail);
                basicBy.TryGetValue(acc.AccountId, out var basicInfo);

                foreach (var e in acc.LedgerEntries)
                {
                    result.Events.Add(new NonSpendOrderReport
                    {
                        CustomerId = basicInfo?.ExtAccountId ?? acc.AccountId,
                        CustomerName = detail?.Event?.DealerCompanyName ?? "",
                        IsActive = detail != null ? !detail.Event.IsDisabled : null,

                        EventType = e.EventType,
                        PointsDeposited = e.PointsDeposited,
                        PointsWithdrawn = e.PointsWithdrawn,
                        EarnDate = e.EarnDate,
                        BurnDate = e.BurnDate,
                        AdminUserId = e.UserId
                    });
                }
            }

            return result;
        }

        // =========================
        // REDEMPTION LOGIC
        // =========================
        private Dictionary<string, decimal> CalculateRedemptions(List<PointLedgerDto> pointLedgers)
        {
            var totals = new Dictionary<string, decimal>();

            foreach (var acc in pointLedgers)
            {
                totals.TryAdd(acc.AccountId, 0);

                foreach (var e in acc.LedgerEntries)
                {
                    if (e.EventType.Equals("redemption", StringComparison.OrdinalIgnoreCase))
                        totals[acc.AccountId] += e.PointsWithdrawn ?? 0;

                    if (e.EventType.Equals("Redemption-canceled", StringComparison.OrdinalIgnoreCase))
                        totals[acc.AccountId] -= e.PointsWithdrawn ?? 0;
                }
            }

            return totals;
        }

        // =========================
        // POINT AGGREGATION
        // =========================
        private Dictionary<string, decimal> BuildPointData(List<PointLedger> points, string accountId)
        {
            var result = new Dictionary<string, decimal>();

            foreach (var p in points.Where(x => x.AccountId == accountId))
            {
                if (!POINT_TYPE_MAP.TryGetValue(p.PointAccountTypeId, out var type))
                    continue;

                result[$"{type}_currentBalance"] = p.CurrentBalance ?? 0;
                result[$"{type}_lifetimeTotal"] = p.LifetimeTotal ?? 0;
            }

            return result;
        }

        private async Task<List<PointLedgerDto>> GetNonOrderEntries(List<PointLedger> pointLedger)
        {
            var results = new List<PointLedgerDto>();

            foreach (var acc in pointLedger)
            {
                var entries = new List<LedgerEntryDto>();

                if (acc.Entries == null) continue;

                foreach (var list in acc.Entries.Values)
                {
                    foreach (var e in list ?? Enumerable.Empty<LedgerEntry>())
                    {
                        if (!e.EventType.Equals("order", StringComparison.OrdinalIgnoreCase))
                        {
                            entries.Add(new LedgerEntryDto
                            {
                                EventType = e.EventType,
                                EntryId = e.EntryId,
                                EventId = e.EventId,
                                PointsDeposited = e.PointsDeposited,
                                PointsWithdrawn = e.PointsWithdrawn,
                                EarnDate = e.EarnDate,
                                BurnDate = e.BurnDate,
                                UserId = e.UserId
                            });
                        }
                    }
                }

                if (entries.Any())
                {
                    results.Add(new PointLedgerDto
                    {
                        AccountId = acc.Id,
                        PointAccountTypeId = acc.PointAccountTypeId,
                        CurrentBalance = acc.CurrentBalance,
                        LifetimeTotal = acc.LifetimeTotal,
                        LedgerEntries = entries
                    });
                }
            }

            return results;
        }

        private async Task<List<TotalSpendDto>> GetTotalSpendData(List<OrderAndRuleStateDto> orders)
        {
            return orders
                .GroupBy(o => o.AccountId)
                .Select(g => new TotalSpendDto
                {
                    LoyaltyAccountId = g.Key,
                    TotalValue = g.Sum(x => x.Event.Items.Sum(i => i.Value)),
                    OrderCount = g.Count()
                }).ToList();
        }

        private async Task<List<YtdSalesDto>> GetYtdSalesData(List<OrderAndRuleStateDto> orders)
        {
            var start = new DateTime(DateTime.UtcNow.Year, 1, 1);

            return orders
                .Where(o => o.Event.InvoiceDate >= start)
                .GroupBy(o => o.AccountId)
                .Select(g => new YtdSalesDto
                {
                    LoyaltyAccountId = g.Key,
                    YtdSalesDollars = g.Sum(x => x.Event.Items.Sum(i => i.Value)),
                    YtdSalesCount = g.Sum(x => x.Event.Items.Sum(i => i.Quantity))
                }).ToList();
        }

    }
}
