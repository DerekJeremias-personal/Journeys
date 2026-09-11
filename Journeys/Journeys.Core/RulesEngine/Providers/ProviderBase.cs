using Journeys.Core.JsonConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers
{
    [JsonConverter(typeof(ProviderBaseJsonConverter))]
    public abstract class ProviderBase
    {
        public abstract string Kind { get; }
    }
}
