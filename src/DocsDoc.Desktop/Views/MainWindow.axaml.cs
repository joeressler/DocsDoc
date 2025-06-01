using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DocsDoc.Desktop.ViewModels;
using DocsDoc.Core.Services;
using System;

namespace DocsDoc.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        LoggingService.LogInfo("Initializing MainWindow");
        try
        {
            InitializeComponent();
            LoggingService.LogInfo("MainWindow InitializeComponent completed");
            
            // Load configuration from single source of truth
            var configService = new ConfigurationService();
            var config = configService.Load();
            string modelPath = config.ModelPath ?? "";
            string dbPath = config.DatabasePath ?? "rag.sqlite";
            
            LoggingService.LogInfo($"Creating MainViewModel with model: {modelPath}, db: {dbPath}");
            var mainVM = new MainViewModel(modelPath, dbPath);
            DataContext = mainVM;
            LoggingService.LogInfo("MainViewModel set as DataContext");

            // Wire up child viewmodels with status callback
            SetupChildViewModels(mainVM);
            LoggingService.LogInfo("MainWindow initialization completed successfully");
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error during MainWindow initialization", ex);
            throw;
        }
    }

    private void SetupChildViewModels(MainViewModel mainVM)
    {
        LoggingService.LogInfo("Setting up child ViewModels");
        try
        {
            var chatView = this.FindControl<UserControl>("ChatView");
            if (chatView != null)
            {
                chatView.DataContext = new ChatViewModel(mainVM.Orchestrator, mainVM.SetStatus, mainVM);
                LoggingService.LogInfo("ChatViewModel setup completed");
            }
            else
            {
                LoggingService.LogInfo("ChatView control not found, skipping ChatViewModel setup");
            }

            var urlView = this.FindControl<UserControl>("UrlIngestionView");
            if (urlView != null)
            {
                urlView.DataContext = new UrlIngestionViewModel(mainVM.WebIngestor, mainVM.SetStatus);
                LoggingService.LogInfo("UrlIngestionViewModel setup completed");
            }
            else
            {
                LoggingService.LogInfo("UrlIngestionView control not found, skipping UrlIngestionViewModel setup");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error setting up child ViewModels", ex);
        }
    }
}