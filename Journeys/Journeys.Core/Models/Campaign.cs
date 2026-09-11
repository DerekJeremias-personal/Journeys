using Journeys.Core.Interfaces.Entities;
using Journeys.Core.RulesEngine.Journey;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class Campaign : TenantedModelBase, IReferenceable
    {
        [JsonConstructor]
        public Campaign(string? extCampaignId, string status, string name, List<string>? events,
            DateTimeOffset startDate, DateTimeOffset? endDate, List<Segment>? segments, JourneyNode? journey,
            string tenantId, string id, DateTimeOffset? deployedDate = null, DateTimeOffset? archivedDate = null) : base(tenantId, id)
        {
            ExtCampaignId = extCampaignId;
            Status = status;
            Name = name;
            Events = events;
            StartDate = startDate;
            EndDate = endDate;
            Segments = segments;
            Journey = journey;
            DeployedDate = deployedDate;
            ArchivedDate = archivedDate;
        }

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalId
        {
            get
            {
                return ExtCampaignId;
            }
        }

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalIdType
        {
            get
            {
                return "Campaign";
            }
        }

        public string? ExtCampaignId { get; set; }

        public string Status { get; set; }
        public string Name { get; set; }

        public List<string>? Events { get; set; }

        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }

        public List<Segment>? Segments { get; set; }

        public JourneyNode? Journey { get; set; }

        public DateTimeOffset? DeployedDate { get; set; }

        public DateTimeOffset? ArchivedDate { get; set; }

    }
    public static class CampaignStatusStrings
    {
        public static readonly string Live = "Live";
        public static readonly string Draft = "Draft";
        public static readonly string Archive = "Archive";
        public static readonly string Pause = "Pause";
    }

}
