namespace Journeys.Infra.DataLake;

public class DataLakeAdapterConfig
{
    public const string SECTION_NAME = "DataLake";
    public bool IsUri { get; init; } = false;
    public required string ConnectionString { get; init; }
    public required string DefaultFileSystem { get; init; }
}
