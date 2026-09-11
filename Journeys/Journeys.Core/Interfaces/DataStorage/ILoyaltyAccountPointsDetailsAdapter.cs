using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ILoyaltyAccountPointsDetailsAdapter
    {
        Task<PointsDetails> FetchPointsDetailsAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType);
        Task<List<PointsDetails>> GetManyPointsDetailsAsync(string tenantId, string loyaltyAccountId, List<(string, string)> events);
        Task<PagedResultSet<PointsDetails>> GetAllAccountPointsDetailsAsync(string tenantId, string loyaltyAccountId, int pageSize, string? continuationToken = null);
        Task<PagedResultSet<PointsDetails>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null);
        Task<PointsDetails> UpsertPointsDetailsAsync(string tenantId, PointsDetails details);
        Task DeletePointsDetailAsync(string tenantId, string loyaltyAccountId, string eventId, string eventType);
        //Task DeletePointsDetailsAsync(string tenantId, PointsDetails pointDetails, LoyaltyAccount loyaltyAccount);
    }
}
