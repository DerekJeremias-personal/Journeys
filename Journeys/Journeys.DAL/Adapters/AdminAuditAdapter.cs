using Azure;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class AdminAuditAdapter : BaseAdapter<AdminAudit>, IAdminAuditAdapter
    {
        private const string ADMIN_AUDIT_MODEL_ID = "e5c57318-29c3-4756-a288-10093a2fbe0b";

        IDynamicDataAdapter _accessibleDataAdapter;
        public AdminAuditAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
            _accessibleDataAdapter = dynAdapter;
        }

        public async Task<AdminAudit> FetchAdminAuditAsync(string tenantId, string auditId, string yearMonth)
        {
            return await base.FetchEntityAsync(tenantId, auditId, ADMIN_AUDIT_MODEL_ID, yearMonth);
        }

        public async Task<PagedResultSet<AdminAudit>> GetAdminAuditsAsync(string tenantId, string yearMonth, int pageSize, string? continuationToken = null)
        {
            var resset = await base.FetchEntityByKeyAsync(tenantId, yearMonth, ADMIN_AUDIT_MODEL_ID, pageSize, null, continuationToken);

            return resset;
        }

        public async Task<PagedResultSet<AdminAudit>> QueryAdminAuditsAsync(string tenantId, string query, Dictionary<string, object> parameters, List<string> monthList, // ie ("202506, 202507")
                                                                            string sortBy, SortOrder sortOrder, int pageSize, string? continuationToken = null, CancellationToken token = default(CancellationToken))
        {
            if (monthList?.Count == 0)
            {
                monthList = new List<string> { DateTimeOffset.UtcNow.ToString("yyyyMM") };
            }

            var monthValues = monthList.Select(m => m.Replace("\"", "")).ToList();
            var arrayString = $"[{string.Join(",", monthValues.Select(m => $"\"{m}\""))}]";
            if (string.IsNullOrEmpty(query))
            {
                query = $"ARRAY_CONTAINS({arrayString}, c.timeofoccurrenceyyyymm)";
            }
            else if (!query.Contains("timeofoccurrenceyyyymm"))
            {
                query += $" AND ARRAY_CONTAINS({arrayString}, c.timeofoccurrenceyyyymm)";
            }

            return await _accessibleDataAdapter.QueryEntitiesAsync<AdminAudit>(tenantId, ADMIN_AUDIT_MODEL_ID, query, parameters, sortBy, sortOrder, pageSize, token, continuationToken, null);
        }

        public async Task<AdminAudit> UpsertAdminAuditAsync(string tenantId, AdminAudit audit)
        {
            return await base.UpsertEntityAsync(tenantId, ADMIN_AUDIT_MODEL_ID, audit, typeof(AdminAudit));
        }

        public async Task DeleteAdminAuditAsync(string tenantId, string id, string yearMonth)
        {
            await base.DeleteEntityAsync(tenantId, ADMIN_AUDIT_MODEL_ID, id, new Dictionary<string, string>
            {
                {"TenantId", tenantId },
                {"timeofoccurrenceyyyymm", yearMonth }
            });
        }

        public async Task DeleteAdminAuditAsync(string tenantId, AdminAudit audit)
        {
            await base.DeleteEntityAsync(tenantId, ADMIN_AUDIT_MODEL_ID, audit);
        }

    }
}
