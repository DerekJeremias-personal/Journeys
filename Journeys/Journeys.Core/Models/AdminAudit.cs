using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class AdminAudit : TenantedModelBase
    {
        [JsonConstructor]
        public AdminAudit(string action, string actionType, string loyaltyMemberId, string adminUserId,
            DateTimeOffset timeOfOccurrence, string comment, string actionJSON, string actionPrettyPrint, 
            string tenantId, string id) : base(tenantId, id ?? Guid.NewGuid().ToString())
        {
            Action = action;
            ActionType = actionType;
            LoyaltyMemberId = loyaltyMemberId;
            AdminUserId = adminUserId;
            TimeOfOccurrence = timeOfOccurrence;
            Comment = comment;
            ActionJSON = actionJSON;
            ActionPrettyPrint = actionPrettyPrint;
        }

        public string Action { get; set; }

        public string ActionType { get; set; }

        public string LoyaltyMemberId { get; set; }

        public string AdminUserId { get; set; }

        public DateTimeOffset TimeOfOccurrence { get; set; }

        public string Comment { get; set; }

        public string ActionJSON { get; set; }
        public string ActionPrettyPrint { get; set; }
        public string TimeOfOccurrenceYYYYMM
        {
            get
            {
                return TimeOfOccurrence.ToString("yyyyMM");
            }
            set
            {
                _ = value;
            }
        }
        


    }
}
