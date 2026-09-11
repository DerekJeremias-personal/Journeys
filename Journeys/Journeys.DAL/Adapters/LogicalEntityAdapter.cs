using Journeys.Core.Interfaces.Entities;
using Journeys.Core.Models;
using Journeys.Core.Interfaces.DataStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class LogicalEntityAdapter : ILogicalEntityAdapter
    {
        private readonly IDynamicDataAdapter _dynAdapter;

        public LogicalEntityAdapter(IDynamicDataAdapter dynAdapter) //, IExternalReferenceAdapter extReferenceAdapter)
        {
            _dynAdapter = dynAdapter;
        }

        public async Task<object> UpsertLogicalEntityAsync(string tenantId, string modelId, JsonElement data)
        {
            return await _dynAdapter.SetLogicalEntityAsync(tenantId, data, modelId).ConfigureAwait(false);
        }
    }
}
