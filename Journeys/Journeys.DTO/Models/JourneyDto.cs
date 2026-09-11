using Journeys.DTO.Interfaces;
using Journeys.DTO.JsonConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class JourneyDto : DtoModelBase
    {
        public string? Name { get; set; }
        public List<JourneyDto>? Children { get; set; } = new List<JourneyDto>();
        public List<RuleSetDto>? Rules { get; set; } = new List<RuleSetDto>();

        public string? RootNodeId { get; set; }

        public JsonElement? Navigation { get; set; }
    }

    public class RuleSetDto : DtoModelBase
    {
        public string? Name { get; set; }
        public JsonElement? RuleJsonElement { get; set; }
        public string? RootRuleDiscriminator { get; set; }
        public JsonElement? OutcomesJsonElement { get; set; }
    }

    public enum NavigationTypeDto
    {
        Entry,
        Exit,
        Transition
    }
}
