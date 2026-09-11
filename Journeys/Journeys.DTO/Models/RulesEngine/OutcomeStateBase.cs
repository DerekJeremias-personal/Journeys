using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.RulesEngine
{
    public class OutcomeStateBaseDto
    {
        public string IssuingOutcomeId { get; set; }
        public string IssuingOutcomeKind { get; set; }
        public string IssuingEventId { get; set; }
        public string IssuingEventType { get; set; }
        public string PointAccountTypeId { get; set; }
        public decimal? PointsDeposited { get; set; }
        public decimal? PointsWithdrawn { get; set; }
        public string? CampaignId { get; set; }
        public string? RuleSetId { get; set; }
        public bool IsAwarded { get; set; }
    }

    public class JourneyStateDto
    {
        public List<string> NodeMemberships { get; private set; } = new List<string>();

        [JsonConstructor]
        public JourneyStateDto(List<string> nodeMemberships)
        {
            NodeMemberships = nodeMemberships;
        }
    }
}
