using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Engine
{
    public class RulesServiceResponse
    {
        public RulesEngineState State { get; set; }
        [JsonConstructor]
        public RulesServiceResponse(RulesEngineState state)
        {
            State = state;
        }
    }
}
