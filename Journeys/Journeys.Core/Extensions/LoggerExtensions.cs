using Microsoft.Extensions.Logging;

public static class LoggerExtensions
{
    public static void LogError(this ILogger logger, Exception ex)
    {
        if (logger == null || ex == null) return;

        // Log error with structured exception details (removes second argument requirement)
        logger.Log(LogLevel.Error, new EventId(), ex, null, (_, _) => ex.Message);
    }
}