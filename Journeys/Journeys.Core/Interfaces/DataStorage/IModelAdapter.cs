using Backend.Dto.Structures.Model;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IModelAdapter
    {
        Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null);

        /// <summary>Deletes a model definition in the backend (HTTP DELETE). 404 if the model does not exist.</summary>
        Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default);

        /// <summary>Loads a single model definition from the backend (HTTP GET).</summary>
        Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default);
    }
}
