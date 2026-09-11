using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class PointSource : TenantedModelBase
    {
        [JsonConstructor]
        public PointSource(string extSourceId, string type, string name, string status,
            double currentBalance, double originalBalance, double maxAward, Schedule expirationSchedule,
            string tenantId, string id) : base(tenantId, id)
        {
            ExtSourceId = extSourceId;
            Type = type;
            Name = name;
            Status = status;
            CurrentBalance = currentBalance;
            OriginalBalance = originalBalance;
            MaxAward = maxAward;
            ExpirationSchedule = expirationSchedule;
        }

        public string ExtSourceId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public double CurrentBalance { get; set; }
        public double OriginalBalance { get; set; }
        public double MaxAward { get; set; }
        public Schedule ExpirationSchedule { get; set; }
    }
}
