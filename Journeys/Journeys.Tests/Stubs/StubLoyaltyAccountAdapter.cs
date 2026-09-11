using Journeys.DTO.Models;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backend.Dto.Structures.Model;



namespace Journeys.Tests.Stubs
{
    internal class StubLoyaltyAccountAdapter : ILoyaltyAccountAdapter
    {
        Dictionary<string, Dictionary<string, LoyaltyAccount>> _accounts = new Dictionary<string, Dictionary<string, LoyaltyAccount>>();
        private readonly object _lock = new object();

        public async Task DeleteLoyaltyAccountAsync(string tenantId, string userId)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && tenant.ContainsKey(userId))
                {
                    tenant.Remove(userId);
                }
            }
        }

        public async Task DeleteLoyaltyAccountAsync(string tenantId, LoyaltyAccount user)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && tenant.ContainsKey(user.Id))
                {
                    tenant.Remove(user.Id);
                }
            }
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountAsync(string tenantId, string userId, string status)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && tenant.ContainsKey(userId))
                {
                    return tenant[userId];
                }
            }
            return null;
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, string extId, string type, string status)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && (tenant.Values?.Any() ?? false))
                {
                    return tenant.Values.FirstOrDefault(x => x.ExtAccountId == extId);
                }
            }
            return null;
        }

        public async Task<LoyaltyAccount> UpsertLoyaltyAccountAsync(string tenantId, LoyaltyAccount account)
        {
            LoyaltyAccount result;
            lock (_lock)
            {
                if (_accounts.ContainsKey(tenantId) && _accounts[tenantId].ContainsKey(account.Id))
                {
                    var existing = _accounts[tenantId][account.Id];
                }

                if (string.IsNullOrEmpty(tenantId) || account == null ||
                    string.IsNullOrEmpty(account.Id)) return null;

                if (!_accounts.ContainsKey(tenantId))
                {
                    _accounts.Add(tenantId, new Dictionary<string, LoyaltyAccount>
                    {
                        { account.Id, account }
                    });
                    result = account;
                }
                else if (!_accounts[tenantId].ContainsKey(account.Id))
                {
                    _accounts[tenantId].Add(account.Id, account);
                    result = account;
                }
                else
                {
                    _accounts[tenantId][account.Id] = account;
                    result = account;
                }

                // Preserve existing journeys if present
                if (_accounts.ContainsKey(tenantId) && _accounts[tenantId].ContainsKey(account.Id))
                {
                    var existing = _accounts[tenantId][account.Id];
                    if (existing.Journeys?.Any() ?? false)
                    {
                        account.Journeys = existing.Journeys;
                    }
                }
            }
            return await Task.FromResult(result);
        }

        public async Task<List<LoyaltyAccount>> GetLoyaltyAccountsAsync(string tenantId, List<string> filters)
        {
            throw new NotImplementedException();
        }

        public async Task<LoyaltyAccount> MoveLoyaltyAccountAsync(string tenantId, LoyaltyAccount account, Dictionary<string, string> newPartition)
        {
            LoyaltyAccount result;
            lock (_lock)
            {
                if (string.IsNullOrEmpty(tenantId) || account == null ||
                    string.IsNullOrEmpty(account.Id)) return null;

                account.Status = "deleted";
                if (!_accounts.ContainsKey(tenantId))
                {
                    _accounts.Add(tenantId, new Dictionary<string, LoyaltyAccount>
                    {
                        { account.Id, account }
                    });
                    result = account;
                }
                else if (!_accounts[tenantId].ContainsKey(account.Id))
                {
                    _accounts[tenantId].Add(account.Id, account);
                    result = account;
                }
                else
                {
                    _accounts[tenantId][account.Id] = account;
                    result = account;
                }

                // Preserve existing journeys if present
                if (_accounts.ContainsKey(tenantId) && _accounts[tenantId].ContainsKey(account.Id))
                {
                    var existing = _accounts[tenantId][account.Id];
                    if (existing.Journeys?.Any() ?? false)
                    {
                        account.Journeys = existing.Journeys;
                    }
                }
            }
            return await Task.FromResult(result);
        }

        public async Task<ExternalReference> FetchAccountExternalReferenceAsync(string tenantId, string extId, string type)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && (tenant.Values?.Any() ?? false))
                {
                    var acct = tenant.Values.FirstOrDefault(x => x.ExtAccountId == extId);
                    if (acct != null)
                        return new ExternalReference(type, "active", extId, acct.Id, tenantId, Guid.NewGuid().ToString());
                }
            }
            return null;
        }

        public async Task<LoyaltyAccount> FetchLoyaltyAccountByExtIdAsync(string tenantId, ExternalReference extRef, string status)
        {
            if (!string.IsNullOrEmpty(tenantId) && _accounts.ContainsKey(tenantId))
            {
                var tenant = _accounts[tenantId];
                if (tenant != null && (tenant.Values?.Any() ?? false))
                {
                    return tenant.Values.FirstOrDefault(x => x.ExtAccountId == extRef.Id);
                }
            }
            return null;
        }

        public Task<List<string>> GetRelatedLoyaltyAccountModelIDsAsync(string tenantId)
        {
            throw new NotImplementedException();
        }

        public Task<List<ModelDto>> GetRelatedLoyaltyAccountModelsAsync(string tenantId)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalReference> UpsertExternalReferenceAsync(string tenantId, ExternalReference extRef)
        {
            throw new NotImplementedException();
        }

        public Task<ExternalReference> FetchAliasAsync(string tenantId, string extId, string type)
        {
            throw new NotImplementedException();
        }
    }
}
