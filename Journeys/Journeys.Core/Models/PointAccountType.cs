using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class PointAccountType : TenantedModelBase
    {
        [JsonConstructor]
        public PointAccountType(string? extAccountId, string status, string name, string? pointSourceId, string ledgerType, decimal? pointsLifespanDays,
            DateTimeOffset? pointsLifespanEndDate, string? expiresToPointAccountTypeId, 
            bool? isSpendable, string? roundingoptionstring, decimal roundingdecimalplaces,
            string tenantId, string id) : base(tenantId, id)
        {
            ExtAccountId = extAccountId;
            Status = status;
            Name = name;
            PointSourceId = pointSourceId;
            LedgerType = ledgerType;
            PointsLifespanDays = pointsLifespanDays;
            PointsLifespanEndDate = pointsLifespanEndDate;

            ExpiresToPointAccountTypeId = expiresToPointAccountTypeId;
            IsSpendable = isSpendable;

            RoundingOptionString = roundingoptionstring;
            if (Enum.TryParse<MidpointRounding>(roundingoptionstring, out MidpointRounding roundingStyle))
            {
                RoundingOption = roundingStyle;
            }
            else
            {
                RoundingOption = MidpointRounding.AwayFromZero;
            }
            RoundingDecimalPlaces = roundingdecimalplaces;
        }

        public string? ExtAccountId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string PointSourceId { get; set; }
        public string LedgerType { get; set; }

        public decimal? PointsLifespanDays { get; set; }
        public DateTimeOffset? PointsLifespanEndDate { get; set; }

        public string? ExpiresToPointAccountTypeId { get; set; }

        public bool? IsSpendable { get; set; }

        [JsonIgnore]
        public MidpointRounding RoundingOption { get; set; } = MidpointRounding.AwayFromZero;
        public string? RoundingOptionString { get; set; }
        public decimal RoundingDecimalPlaces { get; set; } = 0;

    }
}
