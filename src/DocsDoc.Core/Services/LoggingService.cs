using System;
using Serilog;
using Serilog.Events;

namespace DocsDoc.Core.Services
{
    /// <summary>
    /// Provides unified Serilog-based logging functionality for the application.
    /// </summary>
    public static class LoggingService
    {
        private static bool _isConfigured = false;

        /// <summary>
        /// The global Serilog logger instance.
        /// </summary>
        public static ILogger Log { get; private set; } = Serilog.Log.Logger;

        /// <summary>
        /// Configure Serilog for the whole application. Call once at app startup.
        /// </summary>
        /// <param name="logFilePath">Optional log file path. Defaults to logs/app.log</param>
        public static void Configure(string? logFilePath = null, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            if (_isConfigured) return;
            logFilePath ??= "logs/app.log";
            Log = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(logFilePath, rollingInterval: Serilog.RollingInterval.Day)
                .CreateLogger();
            Serilog.Log.Logger = Log;
            _isConfigured = true;
        }

        public static void LogInfo(string message)
        {
            Log.Information(message);
        }

        public static void LogError(string message, Exception ex)
        {
            Log.Error(ex, message);
        }
    }
} 