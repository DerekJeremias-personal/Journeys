using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ILogicalEntityAdapter
    {
        Task<object> UpsertLogicalEntityAsync(string tenantId, string modelId, JsonElement data);
    }
}
