using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Journeys.Core.Services
{
    public class DatawarehouseService : IDatawarehouseService
    {

        private readonly ITenantDataAdapter _tenantDataAdapter;
        private readonly ILogger<DatawarehouseService> _logger;

        public DatawarehouseService(ITenantDataAdapter tenantDataAdapter, ILogger<DatawarehouseService> logger)
        {
            _tenantDataAdapter = tenantDataAdapter;
            _logger = logger;
        }

        public async Task<WarehouseConfigDto> UpsertWarehouseConfigAsync(string tenantId, WarehouseConfigDto warehouseConfigDto, CancellationToken token)
        {
            try
            {
                var tenantDto = await _tenantDataAdapter.GetTenantByNameAsync(
                    tenantId,
                    cancellationToken: token);

                if (tenantDto == null)
                {
                    throw new Exception($"Tenant with id {tenantId} not found.");
                }

                // Update config
                var config = tenantDto.DatawarehouseConfig.WarehouseConfig;

                config.SchemaFullName = warehouseConfigDto.SchemaFullName;
                config.ServicePrincipalName = warehouseConfigDto.ServicePrincipalName;
                config.KeyVaultSecretAppId = warehouseConfigDto.KeyVaultSecretAppId;
                config.KeyVaultSecretPassword = warehouseConfigDto.KeyVaultSecretPassword;
                config.WorkSpaceFolder = warehouseConfigDto.WorkSpaceFolder;

                // Save tenant
                var updatedTenant = await _tenantDataAdapter.SaveTenantAsync(
                    tenantDto,
                    token);

                // Return updated DTO
                var savedConfig = updatedTenant?.DatawarehouseConfig?.WarehouseConfig;

                return new WarehouseConfigDto
                {
                    TenantId = tenantId,
                    ServicePrincipalName = savedConfig?.ServicePrincipalName ?? string.Empty,
                    SchemaFullName = savedConfig?.SchemaFullName ?? string.Empty,
                    KeyVaultSecretAppId = savedConfig?.KeyVaultSecretAppId ?? string.Empty,
                    KeyVaultSecretPassword = savedConfig?.KeyVaultSecretPassword ?? string.Empty,
                    WorkSpaceFolder = savedConfig?.WorkSpaceFolder ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving Databricks config for tenant {tenantId}");
                throw;
            }
        }
        public async Task<PagedResultSetResponse<WarehouseConfigDto>> GetWarehouseConfig(string tenantId)
        {
            try
            {
                var tenant = await _tenantDataAdapter.GetTenantByNameAsync(tenantId);

                if (tenant?.DatawarehouseConfig?.WarehouseConfig == null)
                {
                    return new PagedResultSetResponse<WarehouseConfigDto>
                    {
                        Entities = new List<WarehouseConfigDto>(),
                        ContinuationToken = null,
                        Count = 0
                    };
                }

                var config = tenant.DatawarehouseConfig.WarehouseConfig;

                var databricksConfigDto = new WarehouseConfigDto
                {
                    TenantId = tenantId,
                    ServicePrincipalName = config.ServicePrincipalName ?? string.Empty,
                    KeyVaultSecretAppId = config.KeyVaultSecretAppId ?? string.Empty,
                    KeyVaultSecretPassword = config.KeyVaultSecretPassword ?? string.Empty,
                    SchemaFullName = config.SchemaFullName ?? string.Empty,
                    WorkSpaceFolder = config.WorkSpaceFolder ?? string.Empty
                };

                return new PagedResultSetResponse<WarehouseConfigDto>
                {
                    Entities = new List<WarehouseConfigDto>
                    {
                        databricksConfigDto
                    },
                    ContinuationToken = null,
                    Count = 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error getting Databricks config for tenant {tenantId}");
                throw;
            }
        }


        public async Task<DatabricksNormalizedResult> Normalize(JsonElement root)
        {
            var statementId = root.TryGetProperty("statement_id", out var sid) ? sid.GetString() : null;
            string? state = null;
            if (root.TryGetProperty("status", out var status) && status.TryGetProperty("state", out var st))
                state = st.GetString();

            var truncated = false;
            int? totalRowCount = null;
            var rows = new List<Dictionary<string, object?>>();

            if (!root.TryGetProperty("manifest", out var manifest))
                return new DatabricksNormalizedResult(statementId, state, truncated, totalRowCount, rows);

            if (manifest.TryGetProperty("truncated", out var tr) && tr.ValueKind is JsonValueKind.True or JsonValueKind.False)
                truncated = tr.GetBoolean();

            if (manifest.TryGetProperty("total_row_count", out var trc) && trc.ValueKind == JsonValueKind.Number && trc.TryGetInt32(out var trci))
                totalRowCount = trci;

            if (!manifest.TryGetProperty("schema", out var schema)
                || !schema.TryGetProperty("columns", out var colsEl)
                || colsEl.ValueKind != JsonValueKind.Array)
            {
                return new DatabricksNormalizedResult(statementId, state, truncated, totalRowCount, rows);
            }

            var nameByPosition = new List<(int Position, string Name)>();
            foreach (var col in colsEl.EnumerateArray())
            {
                var position = col.TryGetProperty("position", out var posEl) && posEl.TryGetInt32(out var pos)
                    ? pos
                    : nameByPosition.Count;
                var name = col.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (string.IsNullOrEmpty(name))
                    name = $"column_{position}";
                nameByPosition.Add((position, name));
            }

            nameByPosition.Sort((a, b) => a.Position.CompareTo(b.Position));
            var names = nameByPosition.Select(x => x.Name).ToList();

            if (!root.TryGetProperty("result", out var result))
                return new DatabricksNormalizedResult(statementId, state, truncated, totalRowCount, rows);

            AppendRowsFromResult(result, names, rows);

            return new DatabricksNormalizedResult(statementId, state, truncated, totalRowCount, rows);
        }

        /// <summary>
        /// Handles a single <c>result</c> object (typical wait completion) or an array of chunk results if present.
        /// </summary>
        private void AppendRowsFromResult(JsonElement result, IReadOnlyList<string> names, List<Dictionary<string, object?>> rows)
        {
            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (var chunk in result.EnumerateArray())
                    AppendDataArrayRows(chunk, names, rows);
                return;
            }

            AppendDataArrayRows(result, names, rows);
        }

        private void AppendDataArrayRows(JsonElement resultOrChunk, IReadOnlyList<string> names, List<Dictionary<string, object?>> rows)
        {
            if (!resultOrChunk.TryGetProperty("data_array", out var dataArray) || dataArray.ValueKind != JsonValueKind.Array)
                return;

            foreach (var rowEl in dataArray.EnumerateArray())
            {
                if (rowEl.ValueKind != JsonValueKind.Array)
                    continue;

                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                var i = 0;
                foreach (var cell in rowEl.EnumerateArray())
                {
                    var key = i < names.Count ? names[i] : $"column_{i}";
                    row[key] = JsonElementToCellValue(cell);
                    i++;
                }

                rows.Add(row);
            }
        }

        private object? JsonElementToCellValue(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => el.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => el.TryGetInt64(out var l)
                    ? l
                    : el.TryGetDouble(out var d)
                        ? d
                        : el.TryGetDecimal(out var dec)
                            ? dec
                            : el.GetRawText(),
                _ => el.GetRawText()
            };
        }
        public sealed record DatabricksNormalizedResult(
            string? StatementId,
            string? State,
            bool Truncated,
            int? TotalRowCount,
            IReadOnlyList<IReadOnlyDictionary<string, object?>> Entities);
    }
}
