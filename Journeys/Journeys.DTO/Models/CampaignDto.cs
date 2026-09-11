using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class CampaignDto : DtoModelBase
    {
        public string? ExtCampaignId { get; set; }
        public string Status { get; set; }
        public string Name { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public List<string>? Events { get; set; }
        public List<SegmentDto>? Segments { get; set; }

        public JourneyDto? Journey { get; set; }

        public DateTimeOffset? DeployedDate { get; set; }

        public DateTimeOffset? ArchivedDate { get; set; }

        /// <summary>Populated only on successful upsert; omitted on fetch/list responses.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CampaignUpsertAssistantDigestDto? AssistantDigest { get; set; }
    }

    public class CampaignStatisticsDto
    {
        public string Status { get; set; }

    }
}
