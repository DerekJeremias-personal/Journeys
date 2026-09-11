using Microsoft.Extensions.Logging;
using Serilog;

namespace Journeys.Tests
{
    public static class LoggerFactoryProvider
    {
        private static readonly ILoggerFactory _loggerFactory;

        static LoggerFactoryProvider()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.Debug()
                    .CreateLogger());
            });
        }

        public static ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }
    }
}