using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IAdminAuditService
    {
        //Task<AdminAuditDto> UpsertAdminAuditAsync(string tenantId, AdminAuditDto audit);
        //Task<AdminAuditDto?> GetAdminAuditAsync(string tenantId, string configId);
        //Task<IEnumerable<AdminAuditDto>> GetAdminAuditsAsync(string tenantId, string status);
        //Task<bool> DeleteAdminAuditAsync(string tenantId, string configId);
        Task<AdminAuditDto> FetchAdminAuditAsync(string tenantId, string auditId, string yearMonth);
        Task<AdminAuditDto> UpsertAdminAuditAsync(string tenantId, AdminAuditDto audit);
        Task<AdminAuditDto?> AuditOperation<T>(HttpRequest request, string tenantId, T requestBody, string actionPrettyPrint, bool auditRequired = true);
        Task<bool> RollbackAudit(AdminAuditDto auditDto);
        Task<PagedResultSetResponse<AdminAuditDto>> QueryAsync(string tenantId, QueryAdminAuditsRequest request, CancellationToken cancellationToken = default(CancellationToken));
    }

}
