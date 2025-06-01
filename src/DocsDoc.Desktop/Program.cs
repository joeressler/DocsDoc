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
            LoggingService.Configure(App.AppConfig.Logging?.LogFilePath);
            LoggingService.LogInfo("DocsDoc Desktop starting up...");
            
            var modelPath = App.AppConfig.Model?.Path ?? "";
            var modelContextSize = App.AppConfig.Model?.ContextSize ?? 512;
            var modelGpuLayers = App.AppConfig.Model?.GpuLayerCount ?? 0;
            
            // Test LlamaSharp model loading first
            LoggingService.LogInfo($"Testing LlamaSharp model loading from: {modelPath}");
            if (!System.IO.File.Exists(modelPath))
            {
                LoggingService.LogError($"Model file not found: {modelPath}", new System.IO.FileNotFoundException(modelPath));
                Console.WriteLine($"Model file not found: {modelPath}");
                Console.WriteLine($"Current directory: {System.IO.Directory.GetCurrentDirectory()}");
                Console.WriteLine($"Looking for model at: {System.IO.Path.GetFullPath(modelPath)}");
                return;
            }
            
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)modelContextSize, 
                GpuLayerCount = modelGpuLayers
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
