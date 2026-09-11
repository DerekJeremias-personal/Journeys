using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ITagAdapter
    {
        Task<PagedResultSet<Tag>> FetchEntityTagsAsync(string tenantId, string entityId, string type, int pageSize, string? continuationToken = null);

        Task<List<Tag>> GetEntitiesTagsAsync(string tenantId, string type, List<string> filters);

        Task<Tag> UpsertTagAsync(string tenantId, Tag tag);

        Task DeleteEntityTagsAsync(string tenantId, string entityId, string type);

        Task DeleteTagAsync(string tenantId, Tag tag);
    }
}
