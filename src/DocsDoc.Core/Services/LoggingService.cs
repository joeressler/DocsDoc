using System;
using Serilog;
using Serilog.Events;
using DocsDoc.Core.Models;

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
        /// Configure Serilog for the whole application using strongly-typed LoggingSettings.
        /// </summary>
        public static void Configure(LoggingSettings? settings)
        {
            if (_isConfigured) return;
            var logFilePath = settings?.LogFilePath ?? "logs/app.log";
            var logLevel = ParseLogLevel(settings?.LogLevel?.Default) ?? LogEventLevel.Information;
            var msLevel = ParseLogLevel(settings?.LogLevel?.Microsoft) ?? LogEventLevel.Warning;
            var msHostLevel = ParseLogLevel(settings?.LogLevel?.MicrosoftHostingLifetime) ?? LogEventLevel.Information;
            Log = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .MinimumLevel.Override("Microsoft", msLevel)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", msHostLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Serilog.Log.Logger = Log;
            _isConfigured = true;
        }

        /// <summary>
        /// [Obsolete] Configure Serilog for the whole application. Use Configure(LoggingSettings) instead.
        /// </summary>
        [Obsolete("Use Configure(LoggingSettings) instead.")]
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

        private static LogEventLevel? ParseLogLevel(string? level)
        {
            if (string.IsNullOrWhiteSpace(level)) return null;
            return level.ToLowerInvariant() switch
            {
                "verbose" => LogEventLevel.Verbose,
                "debug" => LogEventLevel.Debug,
                "information" => LogEventLevel.Information,
                "info" => LogEventLevel.Information,
                "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                "fatal" => LogEventLevel.Fatal,
                _ => null
            };
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