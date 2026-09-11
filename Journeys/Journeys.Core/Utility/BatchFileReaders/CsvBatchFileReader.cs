using CsvHelper;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.Utilities;
using Journeys.Core.Models;
using Json.More;
using Microsoft.Azure.Amqp.Framing;
using Serilog;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Journeys.Core.Utility.BatchFileReaders;

public class CsvBatchFileReader(DropboxConfig? dropboxConfig): IBatchFileReader
{
    public async IAsyncEnumerable<JsonElement> GetNextGroupAsync(Stream fileStream)
    {
        if (dropboxConfig == null)
        {
            Log.Debug(($"CsvBatchFileReader::GetNextGroupAsync - No DropboxConfig provided."));
            throw new Exception("Attempting to map CSV file without dropbox config");
        }
        
        var csvConfig = dropboxConfig.FileMap?.Deserialize<CsvConfig>();
        var useDirectMapping = csvConfig == null || csvConfig.HeaderMap.Count == 0;
        
        if (useDirectMapping)
        {
            Log.Debug(($"CsvBatchFileReader::GetNextGroupAsync - No FileMap provided, using direct column name mapping"));
        }
        
        using var streamReader = new StreamReader(fileStream);
        using var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
        
        // Configure CSV reader to handle missing fields gracefully
        csvReader.Context.Configuration.MissingFieldFound = null;
        csvReader.Context.Configuration.HeaderValidated = null;
        csvReader.Context.Configuration.Delimiter = ",";
        csvReader.Context.Configuration.HasHeaderRecord = true;
        csvReader.Context.Configuration.IgnoreBlankLines = true;
        csvReader.Context.Configuration.TrimOptions = CsvHelper.Configuration.TrimOptions.Trim;
        csvReader.Context.Configuration.DetectColumnCountChanges = false;
        csvReader.Context.Configuration.Quote = '"';
        csvReader.Context.Configuration.Escape = '"';
        csvReader.Context.Configuration.Mode = CsvHelper.CsvMode.RFC4180;
        // Don't convert headers to lowercase here - we'll do it manually when creating JSON
        csvReader.Context.Configuration.PrepareHeaderForMatch = args => args.Header;
        csvReader.Context.Configuration.ReadingExceptionOccurred = args => 
        {
            Log.Debug($"CsvBatchFileReader::ReadingExceptionOccurred - Row: {args.Exception.Context.Parser.Row}, Exception: {args.Exception.Message}");
            // Continue reading - don't throw exception
            return false; // Return false to indicate the exception was not handled (continue processing)
        };
        
        // Add a custom BadDataFound handler to log and continue
        csvReader.Context.Configuration.BadDataFound = args =>
        {
            Log.Debug($"CsvBatchFileReader::BadDataFound - Row: {args.Context.Parser.Row}, Field: {args.Field}, RawRecord: {args.Context.Parser.RawRecord}");
            // Continue processing - don't throw exception
        };

        var isGrouped = IsGrouped(csvConfig);
        var isLineBased = isGrouped && csvConfig != null &&
                         csvConfig.GroupConfig?.GroupMode?.ToLower() == "line";
        var isItemBased = isGrouped && !isLineBased;
        var requireFullChunkGrouping = isLineBased && csvConfig != null &&
                                      csvConfig.GroupConfig?.RequireFullChunkGrouping == true;

        await csvReader.ReadAsync();
        csvReader.ReadHeader();
        
        // Debug: Log header information
        if (csvReader.HeaderRecord != null)
        {
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Headers found: {csvReader.HeaderRecord.Length}");
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Header names: {string.Join(", ", csvReader.HeaderRecord)}");
        }
        else
        {
            Log.Warning($"CsvBatchFileReader::GetNextGroupAsync - No headers found in CSV file");
        }
        // Initialize record dictionary for reading CSV rows
        var record = new Dictionary<string, string?>();

        // If full chunk grouping is required, use load-and-group mode
        if (requireFullChunkGrouping)
        {
            await foreach (var groupedElement in LoadAndGroupModeAsync(csvReader, csvConfig, record))
            {
                yield return groupedElement;
            }
            yield break;
        }

        // Otherwise, use existing streaming mode
        JsonNode group = GenerateGroup(csvConfig, isGrouped, isLineBased);
        string? currentGroupKey = null; // Track group key for line-based mode

        int recordCount = 0;
        
        while(await csvReader.ReadAsync())
        {
            if (csvReader.HeaderRecord is null)
            {
                Log.Debug(($"CsvBatchFileReader::GetNextGroupAsync - No Header provided: {dropboxConfig.Directory} - position: {fileStream.Position}"));
                throw new Exception("CSV Batch file missing headers");
            }

            record.Clear();

            // Debug: Log current row information
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Processing row {csvReader.Context.Parser.Row}, ColumnCount: {csvReader.Context.Parser.Count}");
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Raw record: {csvReader.Context.Parser.RawRecord}");
            
            // Use the current row's values directly
            for (int i = 0; i < csvReader.HeaderRecord.Length; i++)
            {
                var header = csvReader.HeaderRecord[i].Trim().ToLower();
                var fieldValue = csvReader.GetField(i)?.Trim();
                record[header] = fieldValue;
                Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Field '{header}' = '{fieldValue}'");
            }
            
            // Debug: Log all available fields in the record
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Available fields in record: {string.Join(", ", record.Keys)}");
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Record values: {string.Join(", ", record.Select(kvp => $"{kvp.Key}='{kvp.Value}'"))}");

            recordCount++;
            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Processing record {recordCount}");

            if (!isGrouped)
            {
                if (useDirectMapping)
                {
                    // Use direct column name mapping - each CSV column becomes a property with the same name
                    Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Using direct mapping for {csvReader.HeaderRecord.Length} headers");
                    
                    foreach (var header in csvReader.HeaderRecord)
                    {
                        if (!record.ContainsKeyIgnoreCase(header) || !record.TryGetValueIgnoreCase(header, out string? fieldValue))
                        {
                            Log.Debug(($"CsvBatchFileReader::GetNextGroupAsync - Missing csv value for: {header}"));
                            continue;
                        }

                        // Convert header to lowercase to match model expectations
                        var jsonFieldName = header.ToLower();
                        group[jsonFieldName] = CreateTypedJsonValue(fieldValue);
                        
                        Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Added to group: {jsonFieldName} = '{fieldValue}'");
                    }
                    
                    Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Group after mapping: {group.ToJsonString()}");
                }
                else
                {
                    // Use configured header mapping
                    foreach (var headerMapItem in csvConfig!.HeaderMap)
                    {
                        var headerName = headerMapItem.HeaderName.ToLower();
                        if (!record.ContainsKeyIgnoreCase(headerName) || !record.TryGetValueIgnoreCase(headerName, out string? rawFeldValue))
                        {
                            Log.Debug(($"CsvBatchFileReader::GetNextGroupAsync - Missing mapped csv (record) value for: {headerMapItem.HeaderName}"));
                            continue;
                        }

                        // Check if this is a JSON field
                        if (headerMapItem.IsJsonField == true && headerMapItem.JsonPropertyMap != null)
                        {
                            // Process as JSON field
                            var jsonString = rawFeldValue ?? "[]";
                            ProcessJsonField(headerMapItem, jsonString, group, csvConfig);
                        }
                        else
                        {
                            // Process as regular field (existing logic)
                            var fieldValue = FormatValue(headerMapItem, rawFeldValue ?? string.Empty);
                            var pathParts = headerMapItem.FieldName.Split('.');

                            JsonNode current = group;
                            for (int i = 0; i < pathParts.Length - 1; i++)
                            {
                                var part = pathParts[i];
                                bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                                if (current[part] == null)
                                {
                                    current[part] = isArray ? new JsonArray() : new JsonObject();
                                }

                                if (isArray)
                                {
                                    var arr = current[part]!.AsArray();
                                    if (arr.Count == 0)
                                        arr.Add(new JsonObject());
                                    current = arr[arr.Count - 1]!;
                                }
                                else
                                {
                                    current = current[part]!.AsObject();
                                }
                            }
                            current[pathParts[^1]] = JsonValue.Create(fieldValue);
                        }
                    }
                }

                yield return JsonDocument.Parse(group.ToJsonString()).RootElement;
                
                group = GenerateGroup(csvConfig, isGrouped, isLineBased);
                continue;
            }

            if (isGrouped && csvConfig != null)
            {
                // Check if this is the first item in the group
                var groupArray = isLineBased
                    ? group.AsArray()
                    : group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray();

                if (groupArray.Count == 0)
                {
                    // First row in group - determine group key
                    if (isLineBased)
                    {
                        // For line-based, extract group key from first row
                        foreach (var groupingHeader in csvConfig.GroupConfig!.GroupingHeaders)
                        {
                            var headerName = groupingHeader.ToLower();
                            if (record.ContainsKey(headerName))
                            {
                                currentGroupKey = record[headerName];
                                break;
                            }
                        }
                    }

                    // Create first line/item
                    var lineObject = CreateCompleteLineObject(csvConfig, record, isLineBased);

                    if (isLineBased)
                    {
                        group.AsArray().Add(lineObject);
                    }
                    else
                    {
                        // Item-based: put non-array fields at group level, array fields in item
                        var item = new JsonObject();
                        foreach (var headerMapItem in csvConfig.HeaderMap)
                        {
                            var headerName = headerMapItem.HeaderName.ToLower();
                            if (!record.ContainsKey(headerName))
                            {
                                Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Missing mapped csv value for: {headerMapItem.HeaderName}");
                                continue;
                            }

                            var pathParts = headerMapItem.FieldName.Split('.');

                            // Check if this is a JSON field
                            if (headerMapItem.IsJsonField == true && headerMapItem.JsonPropertyMap != null)
                            {
                                // JSON fields are always array fields - process into item
                                var jsonString = record[headerName] ?? "[]";
                                ProcessJsonField(headerMapItem, jsonString, item, csvConfig);
                            }
                            else if (pathParts.Length > 1 && csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(pathParts[0]))
                            {
                                // Regular array field - goes into item
                                var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                                JsonNode current = item;
                                for (int i = 1; i < pathParts.Length - 1; i++)
                                {
                                    var part = pathParts[i];
                                    bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                                    if (current[part] == null)
                                    {
                                        current[part] = isArray ? new JsonArray() : new JsonObject();
                                    }

                                    if (isArray)
                                    {
                                        var arr = current[part]!.AsArray();
                                        if (arr.Count == 0)
                                            arr.Add(new JsonObject());
                                        current = arr[arr.Count - 1]!;
                                    }
                                    else
                                    {
                                        current = current[part]!.AsObject();
                                    }
                                }
                                current[pathParts[^1]] = JsonValue.Create(fieldValue);
                            }
                            else
                            {
                                // Non-array field - goes at group level for first item
                                var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                                group[headerMapItem.FieldName] = JsonValue.Create(fieldValue);
                            }
                        }
                        group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray().Add(item.DeepClone());
                    }
                    continue;
                }
            }

            if (csvConfig != null && isGrouped)
            {
                // Check if grouping header value changed
                bool groupChanged = false;
                string? newGroupKey = null;

                foreach (var groupingHeader in csvConfig.GroupConfig!.GroupingHeaders)
                {
                    var headerName = groupingHeader.ToLower();
                    if (!record.ContainsKey(headerName)) continue;

                    var recordGroupValue = record[headerName];

                    if (isLineBased)
                    {
                        // For line-based, compare with stored group key
                        if (currentGroupKey != recordGroupValue)
                        {
                            groupChanged = true;
                            newGroupKey = recordGroupValue;
                        }
                    }
                    else
                    {
                        // For item-based, compare with group-level field
                        var headerFieldName = csvConfig.HeaderMap.Find(item =>
                            item.HeaderName.Equals(groupingHeader, StringComparison.OrdinalIgnoreCase))?.FieldName;
                        if (headerFieldName != null)
                        {
                            var groupValue = group[headerFieldName]?.GetValue<string>();
                            if (groupValue != recordGroupValue)
                            {
                                groupChanged = true;
                            }
                        }
                    }

                    if (groupChanged) break;
                }

                if (groupChanged)
                {
                    // Yield current group and start new one
                    yield return JsonDocument.Parse(group.ToJsonString()).RootElement;
                    group = GenerateGroup(csvConfig, isGrouped, isLineBased);
                    currentGroupKey = newGroupKey;

                    // Process current row as first item of new group
                    var lineObject = CreateCompleteLineObject(csvConfig, record, isLineBased);
                    if (isLineBased)
                    {
                        group.AsArray().Add(lineObject);
                    }
                    else
                    {
                        // Item-based: put fields appropriately
                        var item = new JsonObject();
                        foreach (var headerMapItem in csvConfig.HeaderMap)
                        {
                            var headerName = headerMapItem.HeaderName.ToLower();
                            if (!record.ContainsKey(headerName))
                            {
                                Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Missing mapped csv value for: {headerMapItem.HeaderName}");
                                continue;
                            }

                            var pathParts = headerMapItem.FieldName.Split('.');

                            // Check if this is a JSON field
                            if (headerMapItem.IsJsonField == true && headerMapItem.JsonPropertyMap != null)
                            {
                                // JSON fields are always array fields - process into item
                                var jsonString = record[headerName] ?? "[]";
                                ProcessJsonField(headerMapItem, jsonString, item, csvConfig);
                            }
                            else if (pathParts.Length > 1 && csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(pathParts[0]))
                            {
                                // Regular array field
                                var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                                JsonNode current = item;
                                for (int i = 1; i < pathParts.Length - 1; i++)
                                {
                                    var part = pathParts[i];
                                    bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                                    if (current[part] == null)
                                    {
                                        current[part] = isArray ? new JsonArray() : new JsonObject();
                                    }

                                    if (isArray)
                                    {
                                        var arr = current[part]!.AsArray();
                                        if (arr.Count == 0)
                                            arr.Add(new JsonObject());
                                        current = arr[arr.Count - 1]!;
                                    }
                                    else
                                    {
                                        current = current[part]!.AsObject();
                                    }
                                }
                                current[pathParts[^1]] = JsonValue.Create(fieldValue);
                            }
                            else
                            {
                                // Non-array field - goes at group level
                                var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                                group[headerMapItem.FieldName] = JsonValue.Create(fieldValue);
                            }
                        }
                        group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray().Add(item.DeepClone());
                    }
                    continue;
                }

                // Check if group is empty (shouldn't happen but safety check)
                var groupArrayCheck = isLineBased
                    ? group.AsArray()
                    : group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray();
                if (groupArrayCheck.Count == 0) continue;
            }

            // Add current row to existing group
            if (csvConfig != null && isGrouped)
            {
                var lineObject = CreateCompleteLineObject(csvConfig, record, isLineBased);
                
                if (isLineBased)
                {
                    group.AsArray().Add(lineObject);
                }
                else
                {
                    // Item-based: add item to array (only array fields)
                    var newItem = new JsonObject();
                    foreach (var headerMapItem in csvConfig.HeaderMap)
                    {
                        var headerName = headerMapItem.HeaderName.ToLower();
                        if (!record.ContainsKey(headerName))
                        {
                            Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Missing mapped csv value for: {headerMapItem.HeaderName}");
                            continue;
                        }

                        var pathParts = headerMapItem.FieldName.Split('.');

                        // Check if this is a JSON field
                        if (headerMapItem.IsJsonField == true && headerMapItem.JsonPropertyMap != null)
                        {
                            // JSON fields are always array fields - process into item
                            var jsonString = record[headerName] ?? "[]";
                            ProcessJsonField(headerMapItem, jsonString, newItem, csvConfig);
                        }
                        else if (pathParts.Length > 1 && csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(pathParts[0]))
                        {
                            // Regular array field
                            var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                            JsonNode current = newItem;
                            for (int i = 1; i < pathParts.Length - 1; i++)
                            {
                                var part = pathParts[i];
                                bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                                if (current[part] == null)
                                {
                                    current[part] = isArray ? new JsonArray() : new JsonObject();
                                }

                                if (isArray)
                                {
                                    var arr = current[part]!.AsArray();
                                    if (arr.Count == 0)
                                        arr.Add(new JsonObject());
                                    current = arr[arr.Count - 1]!;
                                }
                                else
                                {
                                    current = current[part]!.AsObject();
                                }
                            }
                            current[pathParts[^1]] = JsonValue.Create(fieldValue);
                        }
                    }
                    group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray().Add(newItem.DeepClone());
                }
            }
        }

        if (isGrouped && csvConfig != null)
        {
            var groupArray = isLineBased
                ? group.AsArray()
                : group[csvConfig.GroupConfig!.ItemFieldName]!.AsArray();

            if (groupArray.Count > 0)
            {
                // Convert JsonNode to JsonElement without double serialization
                var jsonString = group.ToJsonString();
                var jsonDocument = JsonDocument.Parse(jsonString);
                yield return jsonDocument.RootElement;
            }
        }

        Log.Debug($"CsvBatchFileReader::GetNextGroupAsync - Completed processing {recordCount} total records");
    }

    private static bool IsGrouped(CsvConfig? csvConfig)
    {
        return csvConfig != null && !string.IsNullOrWhiteSpace(csvConfig.GroupConfig?.ItemFieldName);
    }
    private static JsonNode GenerateGroup(CsvConfig? csvConfig, bool isGrouped, bool isLineBased)
    {
        if (isGrouped && csvConfig != null)
        {
            if (isLineBased)
            {
                // Line-based: return array directly
                return new JsonArray();
            }
            else
            {
                // Item-based: return object with Items array
                var group = new JsonObject();
                group.Add(csvConfig.GroupConfig!.ItemFieldName, new JsonArray());
                return group;
            }
        }

        return new JsonObject();
    }

    /// <summary>
    /// Creates a complete line object with all fields from the CSV record.
    /// For line-based mode: all fields go into the line object.
    /// For item-based mode: this is used internally but items are structured differently.
    /// </summary>
    private static JsonObject CreateCompleteLineObject(CsvConfig csvConfig, Dictionary<string, string?> record, bool isLineBased)
    {
        var lineObject = new JsonObject();

        foreach (var headerMapItem in csvConfig.HeaderMap)
        {
            var headerName = headerMapItem.HeaderName.ToLower();
            if (!record.ContainsKey(headerName))
            {
                Log.Debug($"CsvBatchFileReader::CreateCompleteLineObject - Missing mapped csv value for: {headerMapItem.HeaderName}");
                continue;
            }

            // Check if this is a JSON field
            if (headerMapItem.IsJsonField == true && headerMapItem.JsonPropertyMap != null)
            {
                // Process as JSON field
                var jsonString = record[headerName] ?? "[]";
                ProcessJsonField(headerMapItem, jsonString, lineObject, csvConfig);
            }
            else
            {
                // Process as regular field (existing logic)
                var fieldValue = FormatValue(headerMapItem, record[headerName] ?? string.Empty);
                var pathParts = headerMapItem.FieldName.Split('.');

                JsonNode current = lineObject;

                // Navigate/create nested structure
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    var part = pathParts[i];
                    bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                    if (current[part] == null)
                    {
                        current[part] = isArray ? new JsonArray() : new JsonObject();
                    }

                    if (isArray)
                    {
                        var arr = current[part]!.AsArray();
                        if (arr.Count == 0)
                            arr.Add(new JsonObject());
                        current = arr[arr.Count - 1]!;
                    }
                    else
                    {
                        current = current[part]!.AsObject();
                    }
                }

                // Set the final field value
                current[pathParts[^1]] = JsonValue.Create(fieldValue);
            }
        }

        return lineObject;
    }

    /// <summary>
    /// Loads all lines from the chunk into memory, then groups them by the configured grouping header.
    /// This mode allows grouping lines even when they are not consecutive in the file.
    /// </summary>
    private async IAsyncEnumerable<JsonElement> LoadAndGroupModeAsync(
        CsvHelper.CsvReader csvReader,
        CsvConfig csvConfig,
        Dictionary<string, string?> record)
    {
        Log.Debug("CsvBatchFileReader::LoadAndGroupModeAsync - Starting load-and-group mode");

        // Phase 1: Load all lines into memory
        var allLines = new List<(JsonObject lineObject, string? groupKey)>();

        int recordCount = 0;
        while (await csvReader.ReadAsync())
        {
            if (csvReader.HeaderRecord is null)
            {
                Log.Warning("CsvBatchFileReader::LoadAndGroupModeAsync - No headers found");
                throw new Exception("CSV Batch file missing headers");
            }

            // Read current row into record dictionary
            record.Clear();
            for (int i = 0; i < csvReader.HeaderRecord.Length; i++)
            {
                var header = csvReader.HeaderRecord[i].Trim().ToLower();
                var fieldValue = csvReader.GetField(i)?.Trim();
                record[header] = fieldValue;
            }

            recordCount++;

            // Create complete line object
            var lineObject = CreateCompleteLineObject(csvConfig, record, isLineBased: true);

            // Extract group key from line object
            var groupKey = ExtractGroupKey(lineObject, csvConfig.GroupConfig!.GroupingHeaders, csvConfig.HeaderMap);

            allLines.Add((lineObject, groupKey));

            Log.Debug("CsvBatchFileReader::LoadAndGroupModeAsync - Loaded line {RecordCount} with group key: {GroupKey}",
                recordCount, groupKey ?? "null");
        }

        Log.Information("CsvBatchFileReader::LoadAndGroupModeAsync - Loaded {LineCount} lines, now grouping", allLines.Count);

        // Phase 2: Group lines by group key
        var groupedLines = allLines
            .GroupBy(x => x.groupKey ?? "null", StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key); // Optional: order groups by key for consistent output

        // Phase 3: Yield each group as a JsonArray
        foreach (var group in groupedLines)
        {
            var groupArray = new JsonArray();

            foreach (var (lineObject, _) in group)
            {
                groupArray.Add(lineObject.DeepClone());
            }

            Log.Information("CsvBatchFileReader::LoadAndGroupModeAsync - Yielding group with key '{GroupKey}' containing {LineCount} lines",
                group.Key, groupArray.Count);

            // Convert to JsonElement
            var jsonString = groupArray.ToJsonString();
            var jsonDocument = JsonDocument.Parse(jsonString);
            yield return jsonDocument.RootElement;
        }

        Log.Debug("CsvBatchFileReader::LoadAndGroupModeAsync - Completed grouping {TotalLines} lines into {GroupCount} groups",
            allLines.Count, groupedLines.Count());
    }

    /// <summary>
    /// Extracts the group key from a line object based on the grouping headers configuration.
    /// </summary>
    private static string? ExtractGroupKey(
        JsonObject lineObject,
        List<string> groupingHeaders,
        List<CsvConfigHeaderMapItem> headerMap)
    {
        foreach (var groupingHeader in groupingHeaders)
        {
            // Find the field name that corresponds to this grouping header
            var headerMapItem = headerMap.FirstOrDefault(item =>
                item.HeaderName.Equals(groupingHeader, StringComparison.OrdinalIgnoreCase));

            if (headerMapItem == null) continue;

            // Extract the value from the line object using the field name
            var fieldName = headerMapItem.FieldName;
            var fieldValue = lineObject[fieldName]?.GetValue<string>();

            if (!string.IsNullOrEmpty(fieldValue))
            {
                return fieldValue;
            }
        }

        return null;
    }

    private static JsonObject GenerateGroupItem(CsvConfig? csvConfig, dynamic record)
    {
        var item = new JsonObject();

        if (csvConfig != null)
        {
            foreach (var headerMapItem in csvConfig.HeaderMap)
            {
                if (!csvConfig.GroupConfig!.ItemHeaders.Contains(headerMapItem.HeaderName)) continue;

                var fieldValue = FormatValue(headerMapItem, record[headerMapItem.HeaderName] ?? string.Empty);
                item.Add(headerMapItem.FieldName, fieldValue);
            }
        }

        return item;
    }

    private static string FormatValue(CsvConfigHeaderMapItem headerMapItem, string value)
    {
        if (!string.IsNullOrWhiteSpace(headerMapItem.DateFormat))
        {
            return ParseDateFormat(headerMapItem.DateFormat, value);
        }

        return value;
    }

    private static string ParseDateFormat(string format, string value)
    {
        // Common date formats to try, similar to CustomDateOnlyConverter
        var commonFormats = new[]
        {
            "yyyy-MM-dd",           // 2025-09-05
            "M/d/yyyy",             // 9/5/2025
            "MM/dd/yyyy",           // 09/05/2025
            "M/d/yy",               // 9/5/25
            "MM/dd/yy",             // 09/05/25
            "yyyy-MM-ddTHH:mm:ss",  // 2025-09-05T14:30:00
            "M/d/yyyy HH:mm:ss",    // 9/5/2025 14:30:00
            "MM/dd/yyyy HH:mm:ss",  // 09/05/2025 14:30:00
            "yyyyMMdd",             // 20250905
            "M-d-yyyy",             // 9-5-2025
            "MM-dd-yyyy"            // 09-05-2025
        };

        // First try the configured format if provided
        if (!string.IsNullOrWhiteSpace(format))
        {
            try
            {
                var date = DateTime.ParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces);
                return date.ToString("u").Replace(" ", "T");
            }
            catch
            {
                Log.Debug("Configured format '{0}' failed for value '{1}', trying common formats", format, value);
            }
        }

        // Try common formats
        foreach (var commonFormat in commonFormats)
        {
            try
            {
                var date = DateTime.ParseExact(value, commonFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces);
                return date.ToString("u").Replace(" ", "T");
            }
            catch
            {
                // Continue to next format
            }
        }

        // If all specific formats fail, try general DateTime parsing as last resort
        try
        {
            var date = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces);
            return date.ToString("u").Replace(" ", "T");
        }
        catch
        {
            Log.Warning("Error while parsing date-format: Value: {0}; Configured Format: {1}", value, format);
            return value;
        }
    }

    private static JsonValue CreateTypedJsonValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return JsonValue.Create<string?>(null);

        // Try to parse as decimal
        if (decimal.TryParse(value, out decimal decimalValue))
            return JsonValue.Create(decimalValue);

        // Try to parse as integer
        if (long.TryParse(value, out long longValue))
            return JsonValue.Create(longValue);

        // Try to parse as boolean
        if (bool.TryParse(value, out bool boolValue))
            return JsonValue.Create(boolValue);

        // Try to parse as TimeSpan (for time fields like "09:00:00")
        if (TimeSpan.TryParse(value, out TimeSpan timeValue))
            return JsonValue.Create(timeValue.ToString());

        // Try to parse as DateTimeOffset (for timezone-aware datetime values)
        if (DateTimeOffset.TryParse(value, out DateTimeOffset dateOffsetValue))
            return JsonValue.Create(dateOffsetValue.ToString("O"));

        // Try to parse as DateTime
        if (DateTime.TryParse(value, out DateTime dateValue))
            return JsonValue.Create(dateValue.ToString("O"));

        // Default to string
        return JsonValue.Create(value);
    }
    
    /// <summary>
    /// Processes a JSON field from CSV: parses JSON string, maps properties using JsonPropertyMap, adds to target array field.
    /// </summary>
    private static void ProcessJsonField(
        CsvConfigHeaderMapItem headerMapItem,
        string jsonString,
        JsonNode targetNode,
        CsvConfig csvConfig)
    {
        try
        {
            // Handle empty/null values
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                jsonString = "[]";
            }

            // Parse JSON
            JsonDocument? jsonDoc = null;
            try
            {
                jsonDoc = JsonDocument.Parse(jsonString);
            }
            catch (JsonException ex)
            {
                Log.Warning("CsvBatchFileReader::ProcessJsonField - Invalid JSON in column {HeaderName}: {Error}", 
                    headerMapItem.HeaderName, ex.Message);
                // Create empty array on error
                CreateEmptyArrayField(headerMapItem, targetNode, csvConfig);
                return;
            }

            var jsonArray = jsonDoc.RootElement;
            
            // Validate it's an array
            if (jsonArray.ValueKind != JsonValueKind.Array)
            {
                Log.Warning("CsvBatchFileReader::ProcessJsonField - JSON field {HeaderName} is not an array (ValueKind: {ValueKind})", 
                    headerMapItem.HeaderName, jsonArray.ValueKind);
                CreateEmptyArrayField(headerMapItem, targetNode, csvConfig);
                return;
            }

            // Navigate to target array field
            var pathParts = headerMapItem.FieldName.Split('.');
            JsonNode arrayParent = targetNode;
            
            // Navigate to parent of array field
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                var part = pathParts[i];
                bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

                if (arrayParent[part] == null)
                {
                    arrayParent[part] = isArray ? new JsonArray() : new JsonObject();
                }

                if (isArray)
                {
                    var arr = arrayParent[part]!.AsArray();
                    if (arr.Count == 0)
                        arr.Add(new JsonObject());
                    arrayParent = arr[arr.Count - 1]!;
                }
                else
                {
                    arrayParent = arrayParent[part]!.AsObject();
                }
            }

            // Get or create the target array field
            var arrayFieldName = pathParts[^1]; // e.g., "items"
            if (arrayParent[arrayFieldName] == null)
            {
                arrayParent[arrayFieldName] = new JsonArray();
            }
            var itemsArray = arrayParent[arrayFieldName]!.AsArray();

            // Process each JSON object in the array
            foreach (var jsonItem in jsonArray.EnumerateArray())
            {
                if (jsonItem.ValueKind != JsonValueKind.Object)
                {
                    Log.Debug("CsvBatchFileReader::ProcessJsonField - Skipping non-object element in JSON array for {HeaderName}", 
                        headerMapItem.HeaderName);
                    continue;
                }

                var mappedItem = new JsonObject();

                // Map each property using JsonPropertyMap
                foreach (var kvp in headerMapItem.JsonPropertyMap!)
                {
                    var jsonPropertyName = kvp.Key; // e.g., "sku", "name", "unit_price"
                    var targetFieldName = kvp.Value; // e.g., "sku", "description", "price"

                    if (jsonItem.TryGetProperty(jsonPropertyName, out var jsonProperty))
                    {
                        // Convert and preserve JSON value type
                        var value = ConvertJsonValue(jsonProperty);
                        mappedItem[targetFieldName] = value;
                    }
                    else
                    {
                        Log.Debug("CsvBatchFileReader::ProcessJsonField - JSON property '{JsonProp}' not found in item for {HeaderName}", 
                            jsonPropertyName, headerMapItem.HeaderName);
                    }
                }

                itemsArray.Add(mappedItem);
            }

            Log.Debug("CsvBatchFileReader::ProcessJsonField - Processed {ItemCount} items from JSON field {HeaderName}", 
                itemsArray.Count, headerMapItem.HeaderName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CsvBatchFileReader::ProcessJsonField - Error processing JSON field {HeaderName}", 
                headerMapItem.HeaderName);
            // Create empty array on error
            CreateEmptyArrayField(headerMapItem, targetNode, csvConfig);
        }
    }
    
    /// <summary>
    /// Creates an empty array field at the target path. Used when JSON parsing fails.
    /// </summary>
    private static void CreateEmptyArrayField(
        CsvConfigHeaderMapItem headerMapItem,
        JsonNode targetNode,
        CsvConfig csvConfig)
    {
        var pathParts = headerMapItem.FieldName.Split('.');
        JsonNode arrayParent = targetNode;
        
        // Navigate to parent of array field
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            var part = pathParts[i];
            bool isArray = csvConfig.ArrayFields != null && csvConfig.ArrayFields.Contains(part);

            if (arrayParent[part] == null)
            {
                arrayParent[part] = isArray ? new JsonArray() : new JsonObject();
            }

            if (isArray)
            {
                var arr = arrayParent[part]!.AsArray();
                if (arr.Count == 0)
                    arr.Add(new JsonObject());
                arrayParent = arr[arr.Count - 1]!;
            }
            else
            {
                arrayParent = arrayParent[part]!.AsObject();
            }
        }

        // Create empty array
        var arrayFieldName = pathParts[^1];
        if (arrayParent[arrayFieldName] == null)
        {
            arrayParent[arrayFieldName] = new JsonArray();
        }
    }
    
    /// <summary>
    /// Converts a JsonElement to JsonValue, preserving the original type.
    /// </summary>
    private static JsonValue ConvertJsonValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                // Try decimal first (for prices), then int/long, then double
                if (element.TryGetDecimal(out decimal dec))
                    return JsonValue.Create(dec);
                if (element.TryGetInt64(out long lng))
                    return JsonValue.Create(lng);
                if (element.TryGetInt32(out int intVal))
                    return JsonValue.Create(intVal);
                return JsonValue.Create(element.GetDouble());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
                return JsonValue.Create<string?>(null);
            default:
                // For objects/arrays, serialize to string or handle differently
                return JsonValue.Create(element.GetRawText());
        }
    }
}
