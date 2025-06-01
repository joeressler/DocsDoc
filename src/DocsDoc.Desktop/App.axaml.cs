using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DocsDoc.Core.Models;
using DocsDoc.Core.Services;
using DocsDoc.Desktop.Views;
using System;

namespace DocsDoc.Desktop;

public partial class App : Application
{
    /// <summary>
    /// Loaded application configuration, accessible globally.
    /// </summary>
    public static Configuration? AppConfig { get; private set; }

    public override void Initialize()
    {
        LoggingService.LogInfo("Initializing Avalonia XAML...");
        AvaloniaXamlLoader.Load(this);
        LoggingService.LogInfo("Avalonia XAML initialized successfully.");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            LoggingService.LogInfo("Framework initialization completed, loading configuration...");
            
            // Load configuration from appsettings.json at startup
            var configService = new ConfigurationService();
            AppConfig = configService.Load();
            LoggingService.LogInfo("Configuration loaded successfully.");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                LoggingService.LogInfo("Creating main window...");
                desktop.MainWindow = new MainWindow();
                LoggingService.LogInfo("Main window created successfully.");
            }
            else
            {
                LoggingService.LogInfo("Non-desktop application lifetime detected.");
            }

            base.OnFrameworkInitializationCompleted();
            LoggingService.LogInfo("Application framework initialization complete.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error during framework initialization", ex);
            throw;
        }
    }
}