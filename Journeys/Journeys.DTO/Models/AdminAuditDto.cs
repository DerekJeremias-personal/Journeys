using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class AdminAuditDto : DtoModelBase
    {
        public string Action { get; set; }

        public string ActionType { get; set; }

        public string LoyaltyMemberId { get; set; }

        public string AdminUserId { get; set; }

        public DateTimeOffset TimeOfOccurrence { get; set; }
        public string Comment { get; set; }
        public string ActionJSON { get; set; }
        public string ActionPrettyPrint { get; set; }
    }
}
