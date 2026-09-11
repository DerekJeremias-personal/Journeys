using Journeys.Core.Models;
using Journeys.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IAdminAuditAdapter
    {
        Task<AdminAudit> FetchAdminAuditAsync(string tenantId, string auditId, string yearMonth);

        Task<PagedResultSet<AdminAudit>> GetAdminAuditsAsync(string tenantId, string yearMonth, int pageSize, string? continuationToken = null);

        Task<PagedResultSet<AdminAudit>> QueryAdminAuditsAsync(string tenantId, string query, Dictionary<string, object> parameters, List<string> monthList, // ie ("202506, 202507")
                                                               string sortBy, SortOrder sortOrder, int pageSize, string? continuationToken = null, CancellationToken token = default(CancellationToken));

        Task<AdminAudit> UpsertAdminAuditAsync(string tenantId, AdminAudit audit);

        Task DeleteAdminAuditAsync(string tenantId, string id, string yearMonth);

        Task DeleteAdminAuditAsync(string tenantId, AdminAudit audit);
    }
}
