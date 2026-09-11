using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers
{
    public abstract class ProviderStateBase : ModelBase
    {
        public abstract string Kind { get; }

        public string ProviderId { get; set; }

        [JsonConstructor]
        protected ProviderStateBase(string providerId)
        {
            ProviderId = providerId;
        }
    }
}
