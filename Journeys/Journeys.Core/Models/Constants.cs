namespace Journeys.Core.Models;

public static class PointLedgerTypeStrings
{
    public static readonly string ESCROW = "Escrow";
    public static readonly string SPENDABLE = "Spendable";
    public static readonly string EXPIRED = "Expired";
    public static readonly string NONSPENDABLE = "NonSpendable";
    public static readonly string ARCHIVE = "Archive";
}

public static class BatchJobStatusStrings
{
    public static readonly string PENDING = "Pending";
    public static readonly string CLAIMING = "Claiming";
    public static readonly string PROCESSING = "Processing";
    public static readonly string COMPLETE = "Complete";
    public static readonly string FAILED = "Failed";
}

public static class DropboxConfigFileTypeStrings
{
    public static readonly string CSV = "CSV";
    public static readonly string JSON = "JSON";
    public static readonly string NDJSON = "NDJSON";
}

public static class JsonModelFilePathStrings
{
    public static readonly string
        CSV = Path.Combine(AppContext.BaseDirectory, "JsonModel", "CSVObjectMap.model.json");
}
