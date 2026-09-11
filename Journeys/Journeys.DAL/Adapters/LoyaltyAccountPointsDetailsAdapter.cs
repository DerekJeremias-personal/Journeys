using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class LoyaltyAccountPointsDetailsAdapter : BaseAdapter<PointsDetails>, ILoyaltyAccountPointsDetailsAdapter
    {
        private const string POINT_DETAILS_MODEL_ID = "b71a2bf1-f727-4fdc-8653-1bfe9471393f";

        public LoyaltyAccountPointsDetailsAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
        }

        public async Task<PointsDetails> FetchPointsDetailsAsync(string tenantId, string loyaltyAccountId, string eventType, string eventId)
        {
            var id = EventKeyUtility.ToEventKey(eventType, eventId);
            return await base.FetchEntityAsync(tenantId, id, POINT_DETAILS_MODEL_ID, loyaltyAccountId);
        }

        public async Task<List<PointsDetails>> GetManyPointsDetailsAsync(string tenantId, string loyaltyAccountId, List<(string, string)> events)
        {
            List<(string, Dictionary<string, string>)> ids = events
                .Select(x => (EventKeyUtility.ToEventKey(x.Item1, x.Item2), new Dictionary<string, string>
                    {
                        { "TenantId", tenantId },
                        { "loyaltyAccountId", loyaltyAccountId }
                    }
                ))
                .ToList();

            return await base.GetManyEntitiesAsync(tenantId, POINT_DETAILS_MODEL_ID, ids);
        }

        public async Task<PagedResultSet<PointsDetails>> GetAllAccountPointsDetailsAsync(string tenantId, string loyaltyAccountId, int pageSize, string? continuationToken = null)
        {
            var detailsres = await this.FetchEntityByKeyAsync(tenantId, loyaltyAccountId, POINT_DETAILS_MODEL_ID, pageSize, null, continuationToken);
            return detailsres;
        }

        public async Task<PointsDetails> UpsertPointsDetailsAsync(string tenantId, PointsDetails pointDetails)
        {
            if (string.IsNullOrEmpty(pointDetails.Id))
                pointDetails.Id = EventKeyUtility.ToEventKey(pointDetails.EventType, pointDetails.EventId);

            return await base.UpsertEntityAsync(tenantId, POINT_DETAILS_MODEL_ID, pointDetails, typeof(PointsDetails));
        }

        public async Task DeletePointsDetailAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType)
        {
            var id = EventKeyUtility.ToEventKey(eventType, eventId);
            await base.DeleteEntityAsync(tenantId, POINT_DETAILS_MODEL_ID, id, new Dictionary<string, string>
            {
                { "TenantId", tenantId },
                { "loyaltyaccountid", loyaltyAccountId }
            });
        }

        //public async Task DeletePointsDetailsAsync(string tenantId, PointsDetails pointDetails, LoyaltyAccount loyaltyAccount)
        //{
        //    if (string.IsNullOrEmpty(pointDetails.Id))
        //        pointDetails.Id = $"{pointDetails.EventType}|{pointDetails.EventType}";

        //    await base.DeleteEntityAsync(tenantId, POINT_DETAILS_MODEL_ID, pointDetails.Id, new Dictionary<string, string>
        //    {
        //        { "TenantId", tenantId },
        //        { "loyaltyaccountid", loyaltyAccount.Id }
        //    });
        //}
    }
}
