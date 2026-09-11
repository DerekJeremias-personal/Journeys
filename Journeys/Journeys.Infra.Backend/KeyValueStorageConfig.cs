using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    public class KeyValueStorageConfig
    {
        public const string SECTION_NAME = "KeyValueStorage";
        public required string BaseUrl { get; init; }
        public required string Key { get; init; }
    }
}
