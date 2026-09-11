using Backend.Dto.Structures.Model;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ILoyaltyAccountAdapter
    {
        Task<LoyaltyAccount> FetchLoyaltyAccountAsync(string tenantId, string accountId, string status);
        Task<ExternalReference> FetchAccountExternalReferenceAsync(string tenantId, string extId, string type);

        Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, string extId, string type, string status);
        Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, ExternalReference extRef, string status);

        Task<ExternalReference> FetchAliasAsync(string tenantId, string extId, string type);

        Task<List<LoyaltyAccount>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters);

        Task<List<string>> GetRelatedLoyaltyAccountModelIDsAsync(string tenantId);
        Task<List<ModelDto>> GetRelatedLoyaltyAccountModelsAsync(string tenantId);

        Task<LoyaltyAccount> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccount account);

        Task<ExternalReference> UpsertExternalReferenceAsync(string tenantId, ExternalReference extRef);

        Task<LoyaltyAccount> MoveLoyaltyAccountAsync(string tenantId, LoyaltyAccount account, Dictionary<string, string> newPartition);

        Task DeleteLoyaltyAccountAsync(string tenantId, string loyaltyAccountId);
    }
}
