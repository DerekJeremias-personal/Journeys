using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;

namespace Journeys.Core.RulesEngine.Engine
{
    public class RulesServiceRequest
    {
        public LoyaltyAccount LoyaltyAccount { get; set; }
        public IList<Campaign> Campaigns { get; set; }
        public Dictionary<string, object> Globals { get; set; }
        public object Payload { get; set; }
        public string PayloadModelId { get; set; }
        public bool CalculateOnly { get; set; }
        public string EventId { get; set; }
        public string EventType{ get; set; }
        public INotificationService? NotificationService { get; set; }
        public bool TreatNotificationSendThrowAsFalse { get; set; }

        public RulesServiceRequest(string payloadModelId, object payload, LoyaltyAccount u, IList<Campaign> campaigns, 
                                    Dictionary<string, object>? globals = null, bool calculateOnly = false, string eventId = null, string eventType = null)
        {
            PayloadModelId = payloadModelId;
            LoyaltyAccount = u;
            Campaigns = campaigns;
            Globals = globals ?? new Dictionary<string, object>();
            Payload = payload;
            CalculateOnly = calculateOnly;
            EventId = eventId;
            EventType = eventType;

        }
    }
}
