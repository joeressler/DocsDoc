using Avalonia;
using System;
using LLama.Common;
using LLama;
using DocsDoc.Core.Services;

namespace DocsDoc.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Load configuration first to get log path
            var configService = new ConfigurationService();
            var config = configService.Load();
            App.AppConfig = config; // Set it early for global access

            // Configure logging using path from config
            LoggingService.Configure(App.AppConfig.Logging);
            LoggingService.LogInfo("DocsDoc Desktop starting up...");
            
            var modelSettings = App.AppConfig.Model;
            // Test LlamaSharp model loading first
            LoggingService.LogInfo($"Testing LlamaSharp model loading from: {modelSettings?.Path}");
            if (modelSettings == null || string.IsNullOrWhiteSpace(modelSettings.Path) || !System.IO.File.Exists(modelSettings.Path))
            {
                LoggingService.LogError($"Model file not found: {modelSettings?.Path}", new System.IO.FileNotFoundException(modelSettings?.Path));
                Console.WriteLine($"Model file not found: {modelSettings?.Path}");
                Console.WriteLine($"Current directory: {System.IO.Directory.GetCurrentDirectory()}");
                Console.WriteLine($"Looking for model at: {System.IO.Path.GetFullPath(modelSettings?.Path ?? "")}");
                return;
            }
            var parameters = new ModelParams(modelSettings.Path)
            {
                ContextSize = (uint)modelSettings.ContextSize,
                GpuLayerCount = modelSettings.GpuLayerCount
            };
            LoggingService.LogInfo("Loading LlamaSharp model...");
            using var model = LLamaWeights.LoadFromFile(parameters);
            LoggingService.LogInfo("LlamaSharp model loaded successfully!");
            
            LoggingService.LogInfo("Starting Avalonia desktop application...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            LoggingService.LogInfo("DocsDoc Desktop application ended.");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Critical error during application startup", ex);
            Console.WriteLine($"Application failed to start: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
