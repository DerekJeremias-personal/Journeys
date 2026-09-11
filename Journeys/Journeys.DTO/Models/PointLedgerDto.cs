using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{

    public class PointLedgerDto : DtoModelBase
    {
        public string ModelId { get; set; }
        public string PointAccountTypeId { get; set; }
        public string AccountId { get; set; }
        public decimal? CurrentBalance { get; set; }
        public decimal? LifetimeTotal { get; set; }
        public decimal? PageNumber { get; set; }
        public List<LedgerEntryDto>? LedgerEntries { get; set; }

        [JsonIgnore]
        public bool ResettleASAP { get; set; }
    }

    public class LedgerEntryDto
    {
        public string? EntryId { get; set; }
        public string Type { get; set; }

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

    }

    public static class LedgerEntryType
    {
        public const string Deposit = "+";
        public const string Withdrawl = "-";
    }
}
