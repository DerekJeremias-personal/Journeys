using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    internal class StubTagAdapter : ITagAdapter
    {
        Dictionary<string, Dictionary<string, PagedResultSet<Tag>>> _userTags = new Dictionary<string, Dictionary<string, PagedResultSet<Tag>>>();

        public async Task DeleteEntityTagsAsync(string tenantId, string entityId, string type)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userTags.ContainsKey(tenantId))
            {
                var tenant = _userTags[tenantId];
                if (tenant != null)
                {
                    if (tenant.ContainsKey(entityId))
                    {
                        var set = tenant[entityId].Entities.Where(x => x.Type == type).ToList();
                        if (set != null)
                            set.ForEach(x => tenant[entityId].Entities.Remove(x));
                    }
                }
            }
            return;
        }

        public async Task DeleteTagAsync(string tenantId, Tag tag)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userTags.ContainsKey(tenantId))
            {
                var tenant = _userTags[tenantId];
                if (tenant != null)
                {
                    if (tenant.ContainsKey(tag.EntityId))
                    {
                        tenant[tag.EntityId].Entities.Remove(tag);
                    }
                }
            }
            return;
        }

        public async Task<PagedResultSet<Tag>> FetchEntityTagsAsync(string tenantId, string entityId, string type, int pageSize, string? continuationToken = null)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userTags.ContainsKey(tenantId))
            {
                var tenant = _userTags[tenantId];
                if (tenant != null && tenant.ContainsKey(entityId))
                {
                    return tenant[entityId];
                }
            }
            return null;
        }

        public Task<List<Tag>> GetEntitiesTagsAsync(string tenantId, string type, List<string> filters)
        {
            throw new NotImplementedException();
        }

        public async Task<Tag> UpsertTagAsync(string tenantId, Tag tag)
        {

            if (string.IsNullOrEmpty(tenantId) || tag == null ||
                string.IsNullOrEmpty(tag.EntityId)) return null;

            if (!_userTags.ContainsKey(tenantId))
            {
                _userTags.Add(tenantId, new Dictionary<string, PagedResultSet<Tag>>
                {
                    { tag.EntityId, new PagedResultSet<Tag> { Entities = new List<Tag> { tag } } }
                });
                return tag;
            }
            if (!_userTags[tenantId].ContainsKey(tag.EntityId))
            {
                _userTags[tenantId].Add(tag.EntityId, new PagedResultSet<Tag> { Entities = new List<Tag> { tag } });
                return tag;
            }
            var storedTag = _userTags[tenantId][tag.EntityId].Entities.FirstOrDefault(x =>
                    (!string.IsNullOrEmpty(x.Id) && !string.IsNullOrEmpty(tag.Id) && x.Id.Equals(tag.Id)) ||
                    (
                        (string.IsNullOrEmpty(x.Name) || (!string.IsNullOrEmpty(x.Name) && !string.IsNullOrEmpty(tag.Name) && x.Name.Equals(tag.Name))) &&
                        (string.IsNullOrEmpty(x.Value) || (!string.IsNullOrEmpty(x.Value) && !string.IsNullOrEmpty(tag.Value) && x.Value.Equals(tag.Value))) &&
                        (string.IsNullOrEmpty(x.Type) || (!string.IsNullOrEmpty(x.Type) && !string.IsNullOrEmpty(tag.Type) && x.Type.Equals(tag.Type)))
                    ));

            if (storedTag == null)
            {
                _userTags[tenantId][tag.EntityId].Entities.Add(tag);
            }
            storedTag = tag;

            return await Task.FromResult(storedTag);
        }
    }
}
