using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.RulesEngine
{
    public class ProviderStateBaseDto : DtoModelBase
    {
        public virtual string Kind { get; private set; } = "Unknown";

        public string ProviderId { get; set; }

        [JsonConstructor]
        public ProviderStateBaseDto(string providerId, string kind)
        {
            ProviderId = providerId;
            Kind = kind;
        }
    }
}
