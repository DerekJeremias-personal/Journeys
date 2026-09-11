using System;
using System.Collections.Generic;

namespace Journeys.DTO.Requests
{
    public class MoveTierRequest
    {
        public string? LoyaltyAccountId { get; set; }
        public string? LoyaltyAccountXReference { get; set; }
        public string TargetCampaignId { get; set; }
        public string TargetJourneyId { get; set; }
        public string Comment { get; set; }
        public string AdminUserId { get; set; }
    }
}
