using Journeys.DTO.Models;

namespace Journeys.DTO.Responses
{
    public class MoveTierResponse
    {
        public LoyaltyAccountDto LoyaltyAccount { get; set; }
        public decimal TierQualificationAmountDeposited { get; set; }
        public decimal DealerSpendableAmountDeposited { get; set; }
        public string PreviousCampaignId { get; set; }
        public string PreviousJourneyId { get; set; }
        public string NewCampaignId { get; set; }
        public string NewJourneyId { get; set; }
    }
}
