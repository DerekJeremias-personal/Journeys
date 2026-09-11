using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class TagAdapter : BaseAdapter<Tag>, ITagAdapter
    {
        private const string TAG_MODEL_ID = "95648d1b-dd71-4783-a411-dd6449166066";

        public TagAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
        }

        public async Task<PagedResultSet<Tag>> FetchEntityTagsAsync(string tenantId, string entityId, string type, int pageSize, string? continuationToken = null)
        {
            var resset = await base.FetchEntityByKeyAsync(tenantId, entityId, TAG_MODEL_ID, pageSize, continuationToken);

            return resset; //?.Where(t => t.Type.Equals(type, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        public async Task<List<Tag>> GetEntitiesTagsAsync(string tenantId, string type, List<string> filters)
        {
            return null;
            //await base.GetEntitiesByFiltersAsync(tenantId, TAG_MODEL_ID, filters).ConfigureAwait(false);
        }

        public async Task<Tag> UpsertTagAsync(string tenantId, Tag tag)
        {
            return await base.UpsertEntityAsync(tenantId, TAG_MODEL_ID, tag, typeof(Tag)).ConfigureAwait(false);
        }

        public async Task DeleteEntityTagsAsync(string tenantId, string entityId, string type)
        {
            await base.DeleteEntityAsync(tenantId, TAG_MODEL_ID, entityId);
        }

        public async Task DeleteTagAsync(string tenantId, Tag tag)
        {
            var pkDictionary = new Dictionary<string, string> { { "tenantId", tenantId }, { "entityid", tag.EntityId } };
            await base.DeleteEntityAsync(tenantId, TAG_MODEL_ID, tag.Id, pkDictionary).ConfigureAwait(false);
        }

    }
}
