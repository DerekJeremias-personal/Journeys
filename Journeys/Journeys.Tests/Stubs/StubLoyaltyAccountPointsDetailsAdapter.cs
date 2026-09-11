using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    public class StubLoyaltyAccountPointsDetailsAdapter : ILoyaltyAccountPointsDetailsAdapter
    {
        public Task DeletePointsDetailAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType)
        {
            throw new NotImplementedException();
        }

        public Task DeletePointsDetailsAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType)
        {
            throw new NotImplementedException();
        }

        public Task DeletePointsDetailsAsync(string tenantId, PointsDetails pointDetails)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResultSet<PointsDetails>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task<PointsDetails> FetchPointsDetailsAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResultSet<PointsDetails>> GetAllAccountPointsDetailsAsync(string tenantId, string loyaltyAccountId, int pageSize, string? continuationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<PointsDetails>> GetManyPointsDetailsAsync(string tenantId, string loyaltyAccountId, List<(string, string)> events)
        {
            throw new NotImplementedException();
        }

        public Task<PointsDetails> UpsertPointsDetailsAsync(string tenantId, PointsDetails details)
        {
            throw new NotImplementedException();
        }
    }
}
