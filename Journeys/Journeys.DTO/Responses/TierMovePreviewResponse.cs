using System.Collections.Generic;

namespace Journeys.DTO.Responses
{
    public class TierMovePreviewResponse
    {
        public decimal TierQualificationAmount { get; set; }
        public decimal DealerSpendableAmount { get; set; }
        public decimal CurrentTierQualificationBalance { get; set; }
        public decimal CurrentDealerSpendableBalance { get; set; }
        public decimal ProjectedTierQualificationBalance { get; set; }
        public decimal ProjectedDealerSpendableBalance { get; set; }
        public bool IsTierDemotion { get; set; }
        public bool IsSameJourney { get; set; }
        public bool HasNegativeAdjustment { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }
}
