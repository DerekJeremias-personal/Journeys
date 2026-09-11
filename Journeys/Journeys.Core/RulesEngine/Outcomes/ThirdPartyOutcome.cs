using Journeys.Core.RulesEngine.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Outcomes
{
    public class ThirdPartyOutcome
    {
        public string? TargetFolder { get; set; }
        public IValueProvider? TargetFolderProvider { get; set; }

        public string? TargetFile { get; set; }
        public IValueProvider? TargetFileProvider { get; set; }

        public string TargetAPIURL { get; set; }
        public IValueProvider? TargetAPIURLProvider { get; set; }

        //Dictionary of Providers
    }
}
