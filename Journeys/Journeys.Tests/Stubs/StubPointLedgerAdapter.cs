using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    internal class StubPointLedgerAdapter : ILoyaltyAccountPointLedgerAdapter
    {
        Dictionary<string, Dictionary<string, PagedResultSet<PointLedger>>> _userLedgers = new Dictionary<string, Dictionary<string, PagedResultSet<PointLedger>>>();

        public bool ThrowOnExpire { get; set; }
        public bool ReturnFalseOnExpire { get; set; }
        public int DelayMilliseconds { get; set; }

        public async Task DeleteLoyaltyAccountLedgerAsync(string tenantId, string userId)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userLedgers.ContainsKey(tenantId))
            {
                var tenant = _userLedgers[tenantId];
                if (tenant != null && tenant.ContainsKey(userId))
                {
                    tenant.Remove(userId);
                }
            }
        }

        public async Task DeleteLoyaltyAccountLedgerAsync(string tenantId, PointLedger ledger)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userLedgers.ContainsKey(tenantId))
            {
                var tenant = _userLedgers[tenantId];
                if (tenant != null && tenant.ContainsKey(ledger.AccountId))
                {
                    tenant.Remove(ledger.AccountId);
                }
            }
        }

        public Task DeleteLoyaltyAccountLedgerAsync(string tenantId, string loyaltyAccountId, string id)
        {
            throw new NotImplementedException();
        }

        public async Task<PagedResultSet<PointLedger>> FetchLoyaltyAccountLedgersAsync(string tenantId, string userId, int pageSize, int? pageNumber = null, string? continuationToken = null)
        {
            if (!string.IsNullOrEmpty(tenantId) && _userLedgers.ContainsKey(tenantId))
            {
                var tenant = _userLedgers[tenantId];
                if (tenant != null && tenant.ContainsKey(userId))
                {
                    return tenant[userId];
                }
            }
            return null;
        }

        public async Task<List<PointLedger>> GetLoyaltyAccountLedgersAsync(string tenantId, List<string> filters)
        {
            throw new NotImplementedException();
        }

        public async Task<PointLedger> UpsertLoyaltyAccountLedgerAsync(string tenantId, PointLedger ledger)
        {
            if (ThrowOnExpire && ledger.PointAccountType?.LedgerType == "EXPIRED")
            {
                throw new Exception("Simulated expiration failure");
            }

            if (DelayMilliseconds > 0)
            {
                await Task.Delay(DelayMilliseconds);
            }

            if (string.IsNullOrEmpty(tenantId) || ledger == null ||
                string.IsNullOrEmpty(ledger.AccountId)) return null;

            if (!_userLedgers.ContainsKey(tenantId))
            {
                _userLedgers.Add(tenantId, new Dictionary<string, PagedResultSet<PointLedger>> 
                { 
                    { ledger.AccountId, new PagedResultSet<PointLedger> { Entities = new List<PointLedger> { ledger } } }
                });
                return ledger;
            }
            if (!_userLedgers[tenantId].ContainsKey(ledger.AccountId))
            {
                _userLedgers[tenantId].Add(ledger.AccountId, new PagedResultSet<PointLedger> { Entities = new List<PointLedger> { ledger } });
                return ledger;
            }
            var storedLedger = _userLedgers[tenantId][ledger.AccountId].Entities.FirstOrDefault(e => e.Id == ledger.Id);
            if (storedLedger == null)
            {
                _userLedgers[tenantId][ledger.AccountId].Entities.Add(ledger);
            }
            storedLedger = ledger;

            return await Task.FromResult(storedLedger);
        }

    }
}
