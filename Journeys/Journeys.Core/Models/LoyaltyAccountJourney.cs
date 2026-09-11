using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class LoyaltyAccountJourney : ModelBase
    {
        public string RootJourneyNodeId { get; set; }
        public List<string> JourneyNodeIds { get; set; }

        [JsonConstructor]
        public LoyaltyAccountJourney(string rootjourneynodeid, List<string>? journeynodeids, string? id = null) : base(id)
        {
            RootJourneyNodeId = rootjourneynodeid;
            JourneyNodeIds = journeynodeids;
        }

    }

    public class LoyaltyAccountJourneyState
    {
        //Dictionary<string, List<string>>? _journeys;

        public string LoyaltyAccountId { get; set; }

        public Dictionary<string, List<string>>? Journeys { get; set; }
        //{
        //    get
        //    {
        //        if (_journeys == null)
        //        {
        //            _journeys = JourneysList
        //                        ?.GroupBy(j => j.RootJourneyNodeId)
        //                        ?.ToDictionary(
        //                            group => group.Key,
        //                            group => group.SelectMany(g => g.JourneyNodeIds).ToList()
        //                        );
        //        }
        //        return _journeys;
        //    }
        //    set
        //    {
        //        _journeys = value;
        //        JourneysList = _journeys
        //            ?.Keys?.Select(j => new LoyaltyAccountJourney(j, _journeys[j]))
        //            ?.ToList();
        //    }
        //}



        //[JsonConstructor]

        public LoyaltyAccountJourneyState(string loyaltyAccountId, List<LoyaltyAccountJourney>? journeysList = null)
        {
            //JourneysList = journeysList;
            LoyaltyAccountId = loyaltyAccountId;
            Journeys = journeysList
                        ?.GroupBy(j => j.RootJourneyNodeId)
                        ?.ToDictionary(
                            group => group.Key,
                            group => group.SelectMany(g => g.JourneyNodeIds).ToList()
                        );
        }

        //public bool ContainsKey(string key)
        //{
        //    return Journeys.Any(j => j.RootJourneyNodeId.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        //}
        //public List<string>? GetKey(string key)
        //{
        //    return Journeys
        //        ?.FirstOrDefault(j => j.RootJourneyNodeId.Equals(key, StringComparison.InvariantCultureIgnoreCase))
        //        ?.JourneyNodeIds;
        //}
        //public void Add(string key, List<string> states)
        //{
        //    Journeys = Journeys ?? new List<LoyaltyAccountJourney>();
        //    Journeys.Add(new LoyaltyAccountJourney(key, states));
        //}
        //public void AddSubState(string key, string newid)
        //{
        //    Journeys = Journeys ?? new List<LoyaltyAccountJourney>();
        //    var journey = Journeys.FirstOrDefault(j => j.RootJourneyNodeId.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        //    if (journey != null)
        //    {
        //        journey.JourneyNodeIds = journey.JourneyNodeIds ?? new List<string>();
        //        journey.JourneyNodeIds.Add(newid);
        //    }
        //    else
        //    {
        //        Journeys.Add(new LoyaltyAccountJourney(key, new List<string> { newid }));
        //    }
        //}

        //public void SetKey(string key, List<string> states)
        //{
        //    Journeys = Journeys ?? new List<LoyaltyAccountJourney>();
        //    var journey = Journeys.FirstOrDefault(j => j.RootJourneyNodeId.Equals(key, StringComparison.InvariantCultureIgnoreCase));
        //    journey.JourneyNodeIds = states; //not fault tolerant to honor dictionary behavior
        //}
    }


}
