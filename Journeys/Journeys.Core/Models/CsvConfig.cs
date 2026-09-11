namespace Journeys.Core.Models;

public class CsvConfig
{
    public CsvConfigGroupConfig? GroupConfig { get; set; }
    
    public required List<CsvConfigHeaderMapItem> HeaderMap { get; set; }
    
    public List<string>? ArrayFields { get; set; }
}

public class CsvConfigGroupConfig
{
    public required List<string> GroupingHeaders { get; set; }
    
    public required string ItemFieldName { get; set; }
    
    public required List<string> ItemHeaders { get; set; }

    /// <summary>
    /// Determines how grouped lines are structured.
    /// "item" (default): Creates object with group-level fields and Items array containing item-level fields.
    /// "line": Creates array of complete line objects, each with all fields including Items array.
    /// </summary>
    public string? GroupMode { get; set; } // "item" or "line", defaults to "item"

    /// <summary>
    /// When true, loads all lines in the chunk into memory before grouping.
    /// This allows grouping by SFOQ_ID even when lines are not consecutive in the file.
    /// Only applies when GroupMode is "line".
    /// </summary>
    public bool? RequireFullChunkGrouping { get; set; } // defaults to false

}

public class CsvConfigHeaderMapItem
{
    public required string HeaderName { get; set; }
    
    public required string FieldName { get; set; }
    
    public string? DateFormat { get; set; }
    
    /// <summary>
    /// When true, indicates this CSV column contains a JSON string that should be parsed.
    /// The JSON string should be an array of objects that will be mapped to the target array field.
    /// </summary>
    public bool? IsJsonField { get; set; }
    
    /// <summary>
    /// Maps JSON object property names to target field names.
    /// Key = JSON property name (e.g., "sku", "name", "unit_price")
    /// Value = Target field name in the item object (e.g., "sku", "description", "price")
    /// If a JSON property is not in this map, it will be ignored.
    /// Only used when IsJsonField is true.
    /// </summary>
    public Dictionary<string, string>? JsonPropertyMap { get; set; }
}
