using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class InitStateEntry
    {
        public string AccountXId { get; set; }
        public List<InitPointState> PointStates { get; set; }
        public List<InitJourneyState> JourneyStates { get; set; }

        public InitStateEntry(string accountXId)
        {
            AccountXId = accountXId;
            PointStates = new List<InitPointState>();
            JourneyStates = new List<InitJourneyState>();
        }

        [JsonConstructor()]
        public InitStateEntry(string accountXId, List<InitPointState> pointStates, List<InitJourneyState> journeyStates)
        {
            AccountXId = accountXId;
            PointStates = pointStates ?? new List<InitPointState>();
            JourneyStates = journeyStates ?? new List<InitJourneyState>();
        }
    }

    public class InitPointState
    {
        public string PointAccountTypeId { get; set; }
        public decimal Balance { get; set; }

        [JsonConstructor()]
        public InitPointState(string pointAccountTypeId, decimal balance)
        {
            PointAccountTypeId = pointAccountTypeId;
            Balance = balance;
        }
    }

    public class  InitJourneyState
    {
        public string CampaignId { get; set; }
        public List<string> JourneyIds { get; set; }

        [JsonConstructor()]
        public InitJourneyState(string campaignId, List<string> journeyIds)
        {
            CampaignId = campaignId;
            JourneyIds = journeyIds ?? new List<string>();
        }
    }
}
