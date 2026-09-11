using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Journeys.DTO.Responses;
using Journeys.DTO.Requests;
using System.Text.Encodings.Web;
using System.Text.Json;
using Journeys.Core.Extensions;

namespace Journeys.Core.Services
{
    public class AdminAuditService : IAdminAuditService
    {
        protected ILogger<IAdminAuditService> Logger { get; set; }
        protected IAdminAuditAdapter AuditAdapter { get; set; }
        public AdminAuditService(ILogger<AdminAuditService> logger, IAdminAuditAdapter adminAuditAdapter) 
        { 
            Logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
            AuditAdapter = adminAuditAdapter ?? throw new ArgumentNullException(nameof(adminAuditAdapter), "AdminAuditAdapter cannot be null.");
        }
        public async Task<AdminAuditDto?> AuditOperation<T>(HttpRequest request, string tenantId, T requestBody, string actionPrettyPrint, bool auditRequired = true)
        {
            if (!request.Headers.TryGetValue("X-Journeys-Audit", out var auditJson)
                || string.IsNullOrWhiteSpace(auditJson)) 
            {
                if (auditRequired)
                {
                    throw new APIErrorsException(new Dictionary<string, string>
                    {
                        { "X-Journeys-Audit", "The X-Journeys-Audit header is required for audit operations." }
                    });
                }
                return default(AdminAuditDto);
            }

            try
            {
                var audit = JsonUtility.Deserialize<AdminAuditDto>(auditJson, Logger);
                if (audit == null)
                {
                    if (auditRequired)
                    {
                        throw new APIErrorsException(new Dictionary<string, string>
                        {
                            { "X-Journeys-Audit", "The evaluated X-Journeys-Audit header was empty or null, this is required for audited operations." }
                        });
                    }
                    return default(AdminAuditDto);
                }

                audit.TenantId = tenantId;
                var actionJSON = JsonUtility.Serialize(requestBody, Logger);
                //Base 64 String Encode the actionJSON strin value.
                var base64ActionJSON = Convert.ToBase64String(Encoding.UTF8.GetBytes(actionJSON));
                audit.ActionJSON = base64ActionJSON;
                audit.TimeOfOccurrence = DateTime.UtcNow;
                audit.ActionPrettyPrint = actionPrettyPrint;

                var entity = audit.FromDto();
                if(entity == null)
                {
                    throw new APIErrorsException(new Dictionary<string, string>
                    {
                        { "X-Journeys-Audit", "The X-Journeys-Audit header could not be converted from AdminAuditDTO to an AdminAudit entity." }
                    });
                }

                var savedAudit = await AuditAdapter.UpsertAdminAuditAsync(tenantId, entity).ConfigureAwait(false);
                if(savedAudit == null)
                {
                    throw new APIErrorsException(new Dictionary<string, string>
                    {
                        { "X-Journeys-Audit", "The X-Journeys-Audit header could not be saved to the database." }
                    });
                }
                return savedAudit?.ToDto();
            }
            catch (Exception ex)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "X-Journeys-Audit", $"The X-Journeys-Audit header is not a valid JSON object: {ex.Message}" }
                });
            }
        }

        public async Task<bool> RollbackAudit(AdminAuditDto auditDto)
        {
            if(auditDto == null)
            {
                return true; // Nothing to rollback, so return true
            }

            var audit = auditDto.FromDto();
            if(audit == null)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "X-Journeys-Audit Rollback Failure", "The provided AdminAuditDto could not be converted to an AdminAudit entity for rollback." }
                });
            }
            await AuditAdapter.DeleteAdminAuditAsync(audit.TenantId, audit.Id, audit.TimeOfOccurrenceYYYYMM).ConfigureAwait(false);
            return true;
        }


        public async Task<AdminAuditDto> FetchAdminAuditAsync(string tenantId, string auditId, string yearMonth)
        {
            AdminAuditDto audit = null;
            try
            {
                var res = await AuditAdapter.FetchAdminAuditAsync(tenantId, auditId, yearMonth);
                return res.ToDto();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
            return audit;
        }

        public async Task<AdminAuditDto> UpsertAdminAuditAsync(string tenantId, AdminAuditDto audit)
        {
            try
            {
                var auditToStore = audit.FromDto();

                var res = await AuditAdapter.UpsertAdminAuditAsync(tenantId, auditToStore);
                audit = res.ToDto();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
            return audit;
        }
        public async Task<PagedResultSetResponse<AdminAuditDto?>> QueryAsync(string tenantId, QueryAdminAuditsRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            var query = request.Query;
            var parameters = request.Parameters ?? new Dictionary<string, object>();
            DateTimeOffset startDt = request.StartDateRange ?? DateTimeOffset.UtcNow.AddMonths(-1);
            var monthList = startDt.GetMonthRangeAsYYYYMMList(request.EndDateRange ?? DateTimeOffset.UtcNow);
            
            var result = await AuditAdapter.QueryAdminAuditsAsync(tenantId, query, parameters, monthList, request.SortBy, request.SortOrder, request.PageSize, request.ContinuationToken, cancellationToken);

            if (result == null)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "Query Error", "The query returned no results and/or failed to execute." }
                });
            }

            if (result.Entities == null || !result.Entities.Any())
            {
                return new PagedResultSetResponse<AdminAuditDto?>
                {
                    Entities = new List<AdminAuditDto?>(),
                    ContinuationToken = result.ContinuationToken,
                    Count = 0
                };
            }

            return new PagedResultSetResponse<AdminAuditDto?>
            {
                Entities = result.Entities.Select(x => x.ToDto()).ToList(),
                ContinuationToken = result.ContinuationToken,
                Count = result.Count
            };
        }
    }

    public static class DateTimeExtensions
    {
        /// <summary>
        /// Generates a comma-separated list of "yyyyMM" formatted months between start and end dates (inclusive)
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>Comma-separated string of yyyyMM formatted months</returns>
        public static string GetMonthRangeAsYYYYMM(this DateTimeOffset startDate, DateTimeOffset endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            var months = new List<string>();
            var currentDate = new DateTimeOffset(startDate.Year, startDate.Month, 1, 0, 0, 0, startDate.Offset);
            var endMonth = new DateTimeOffset(endDate.Year, endDate.Month, 1, 0, 0, 0, endDate.Offset);

            while (currentDate <= endMonth)
            {
                months.Add($"{currentDate.ToString("yyyyMM")}");
                currentDate = currentDate.AddMonths(1);
            }

            return string.Join(",", months);
        }

        /// <summary>
        /// Generates a list of "yyyyMM" formatted months between start and end dates (inclusive)
        /// </summary>
        /// <param name="startDate">The start date</param>
        /// <param name="endDate">The end date</param>
        /// <returns>List of yyyyMM formatted months</returns>
        public static List<string> GetMonthRangeAsYYYYMMList(this DateTimeOffset startDate, DateTimeOffset endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            var months = new List<string>();
            var currentDate = new DateTimeOffset(startDate.Year, startDate.Month, 1, 0, 0, 0, startDate.Offset);
            var endMonth = new DateTimeOffset(endDate.Year, endDate.Month, 1, 0, 0, 0, endDate.Offset);

            while (currentDate <= endMonth)
            {
                months.Add($"{currentDate.ToString("yyyyMM")}");
                currentDate = currentDate.AddMonths(1);
            }

            return months;
        }

        /// <summary>
        /// Gets the "yyyyMM" formatted string for a DateTimeOffset
        /// </summary>
        /// <param name="date">The date to format</param>
        /// <returns>yyyyMM formatted string</returns>
        public static string ToYYYYMM(this DateTimeOffset date)
        {
            return date.ToString("yyyyMM");
        }
    }
}
