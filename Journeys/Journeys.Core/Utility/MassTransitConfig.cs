namespace Journeys.Core.Utility;

public class MassTransitConfig
{
    public const string SECTION_NAME = "MassTransit";
    public bool UseLocalServices { get; init; } = false;
    public MassTransitTransportConfig Transport { get; init; } = new();
    public MassTransitSagaRepositoryConfig SagaRepository { get; init; } = new();
    public JobOptionsConfig JobOptions { get; set; } = new();
}
public class JobOptionsConfig
{
    public int JobTimeoutMinutes { get; set; } = 180;
    public int ConcurrentJobLimit { get; set; } = 20;
}

public class MassTransitTransportConfig
{
    public bool IsUri { get; init; } = true;
    public string Host { get; init; } = string.Empty;
}

public class MassTransitSagaRepositoryConfig
{
    public bool IsEmulator { get; init; } = false;
    public string Host { get; init; } = string.Empty;
    public string Database { get; init; } = "masstransit";
    public string Collection { get; init; } = "sagas";

    public string? Key { get; set; }
}
