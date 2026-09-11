using System.Text;
using System.Text.Json;
using Backend.Dto.Dynamic;
using Journeys.Core.RulesEngine.Providers;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.Services
{
    public interface IExportService
    {
        string GenerateCsv(IEnumerable<EventPayloadResponseDto> entities, ExportByQueryRequest request);
    }

    public class ExportService : IExportService
    {
        private readonly ILogger<ExportService> _logger;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        public string GenerateCsv(IEnumerable<EventPayloadResponseDto> entities, ExportByQueryRequest request)
        {
            var csv = new StringBuilder();
            
            // Add headers
            csv.AppendLine(string.Join(",", request.Columns.Select(c => $"\"{c.Header}\"")));
            // Add rows
            foreach (var entity in entities)
            {
                try
                {
                    var dynamicData = DynamicHelper.Import(entity);
                    var row = request.Columns.Select(column =>
                    {
                        try
                        {
                            var provider = new PathValueProvider(column.Field);
                            var value = provider.GetValue<object>(dynamicData);
                            return FormatValue(value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing column {Column} for entity {EntityId}", 
                                column.Field, entity.LoyaltyAccountId);
                            return "\"\"";
                        }
                    });
                    
                    csv.AppendLine(string.Join(",", row));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing entity {EntityId}", entity.LoyaltyAccountId);
                    // Continue with next entity
                }
            }

            return csv.ToString();
        }

        private string FormatValue(object value)
        {
            if (value == null) return "\"\"";
            
            var stringValue = value.ToString();
            // Escape quotes and wrap in quotes
            return $"\"{stringValue.Replace("\"", "\"\"")}\"";
        }
    }
} 