using Journeys.Core.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Journeys.Core.Models
{
    public class PointLedger : TenantedModelBase
    {
        [JsonConstructor]
        public PointLedger(string accountId, string pointAccountTypeId, decimal? currentBalance, decimal? lifetimeTotal, decimal? pagenumber,
                           Dictionary<string, List<LedgerEntry>>? entries, string tenantId, string id) : base(tenantId, id)
        {
            AccountId = accountId;
            PointAccountTypeId = pointAccountTypeId;
            CurrentBalance = currentBalance;
            LifetimeTotal = lifetimeTotal;
            PageNumber = pagenumber;
            Entries = entries;

            //After setting properties (notably PointAccountTypeId)
            // initialize PointAccountType for lazy loading
            InitializePointAccountType();
        }
        //public string ModelId { get; set; }
        public string PointAccountTypeId { get; set; }

        public string AccountId { get; set; }

        public decimal? CurrentBalance { get; set; }
        public decimal? LifetimeTotal { get; set; }
        public decimal? PageNumber { get; set; } = 1;

        /// <summary>
        /// Here the Key is the EventType|EventId
        /// </summary>
        public Dictionary<string, List<LedgerEntry>>? Entries { get; set; }

        private Task<PointAccountType> _pointAccountTypeTask;

        [JsonIgnore]
        public PointAccountType? PointAccountType
        {
            get => _pointAccountTypeTask?.Result;
            set => _pointAccountTypeTask = Task.FromResult(value);
        }

        public void InitializePointAccountType()
        {
            if (_pointAccountTypeTask == null && !string.IsNullOrEmpty(PointAccountTypeId))
            {
                _pointAccountTypeTask = PointAccountTypeCache.Instance.GetPointAccountTypeAsync(this.TenantId, PointAccountTypeId);
            }
        }
        
        public PointLedger Clone()
        {
            return new PointLedger(
                AccountId,
                PointAccountTypeId,
                CurrentBalance,
                LifetimeTotal,
                PageNumber,
                Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(entry => entry.Clone()).ToList()
                ),
                TenantId,
                Id
            );
        }

    }

    public class LedgerEntry
    {
        [JsonConstructor]
        public LedgerEntry(string type, string? entryId, string eventType, string eventId, string? outcomeId, decimal? pointsDeposited = null,
            decimal? pointsWithdrawn = null, decimal? spendablePoints = null, DateTimeOffset? expirationDate = null,
            DateTimeOffset? earnDate = null, DateTimeOffset? burnDate = null, List<string>? spendReferences = null,
            string? status = null, string? userId = null)
        {
            Type = type;
            EntryId = entryId;
            EventType = eventType;
            EventId = eventId;
            OutcomeId = outcomeId;
            PointsDeposited = pointsDeposited;
            PointsWithdrawn = pointsWithdrawn;
            SpendablePoints = spendablePoints;
            ExpirationDate = expirationDate;
            EarnDate = earnDate;
            BurnDate = burnDate;
            SpendReferences = spendReferences;
            Status = status;
            UserId = userId;
        }

        public string Type { get; set; }
        public string? EntryId { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }

        public string? OutcomeId { get; set; }
        public decimal? PointsDeposited { get; set; }
        public decimal? PointsWithdrawn { get; set; }
        public decimal? SpendablePoints { get; set; }
        public DateTimeOffset? ExpirationDate { get; set; }
        public DateTimeOffset? EarnDate { get; set; }
        public DateTimeOffset? BurnDate { get; set; }
        public List<string>? SpendReferences { get; set; }

        public string? Status { get; set; }
        public string? UserId { get; set; }

        public LedgerEntry Clone()
        {
            return new LedgerEntry(
                Type,
                EntryId,
                EventType,
                EventId,
                OutcomeId,
                PointsDeposited,
                PointsWithdrawn,
                SpendablePoints,
                ExpirationDate,
                EarnDate,
                BurnDate,
                SpendReferences,
                Status,
                UserId
            );
        }
    }

    public sealed class PointLedgerType
    {
        public string Value { get; private set; }

        private PointLedgerType(string value) => Value = value;

        public static readonly PointLedgerType ESCROW = new PointLedgerType(PointLedgerTypeStrings.ESCROW);
        public static readonly PointLedgerType SPENDABLE = new PointLedgerType(PointLedgerTypeStrings.SPENDABLE);
        public static readonly PointLedgerType EXPIRED = new PointLedgerType(PointLedgerTypeStrings.EXPIRED);
        public static readonly PointLedgerType NONSPENDABLE = new PointLedgerType(PointLedgerTypeStrings.NONSPENDABLE);
        public static readonly PointLedgerType ARCHIVE = new PointLedgerType(PointLedgerTypeStrings.ARCHIVE);

        public static IEnumerable<PointLedgerType> List() => new[] { ESCROW, SPENDABLE, EXPIRED, NONSPENDABLE, ARCHIVE };

        public static PointLedgerType FromValue(string value)
        {
            return List().FirstOrDefault(x => x.Value == value)
                   ?? throw new ArgumentException($"Invalid PointLedger ledgerTypeId: {value}");
        }

        public override string ToString() => Value;
    }

}
