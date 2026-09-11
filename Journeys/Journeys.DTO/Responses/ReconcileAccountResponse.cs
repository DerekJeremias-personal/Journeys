using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses
{
    public class ReconcileAccountResponse
    {
        public string ExternalId { get; set; }
        public string LoyaltyAccountId { get; set; }

        public string ModelName { get; set; }

        public int TotalEvents { get; set; }
        public int TotalEventsUnprocessed { get; set; }
        public int TotalEventsReprocessed { get; set; }

        public int TotalEventsToBeReprocessed { get; set; }

        public List<ReconciliationDetail> ReconciliationDetails { get; set; }

        public DateTimeOffset RunDate { get; set; }
    }

    public class ReconciliationDetail
    {
        public string EventKey { get; set; }
        public string EventType { get; set; }

        public DateTimeOffset EventDate { get; set; }

        public DateTimeOffset? EventProcessedDate { get; set; }

        private string _eventProcessingStatus = EventProcessingStatuses.UNPROCESSED;
        
        public string EventProcessingStatus
        {
            get { return _eventProcessingStatus; }
            private set { _eventProcessingStatus = value; }
        }

        public bool OutcomesAwarded { get; set; }

        public decimal PointAward { get; set; }

        public string JourneyNodeAtEvent { get; set; }

        public Dictionary<string, string> Errors { get; set; }

        public void SetProcessedStatus()
        {
            EventProcessingStatus = EventProcessingStatuses.PROCESSED;
        }
        public void SetUnprocessedStatus()
        {
            EventProcessingStatus = EventProcessingStatuses.UNPROCESSED;
        }
        public void SetReprocessedStatus()
        {
            EventProcessingStatus = EventProcessingStatuses.REPROCESSED;
        }
    }

    public static class EventProcessingStatuses
    {
        public static string PROCESSED = "Processed";
        public static string UNPROCESSED = "Unprocessed";
        public static string REPROCESSED = "Reprocessed";
    }

}
