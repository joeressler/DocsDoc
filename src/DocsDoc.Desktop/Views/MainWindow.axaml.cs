using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DocsDoc.Desktop.ViewModels;
using DocsDoc.Core.Services;
using System;

namespace DocsDoc.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly ConfigurationService _configService;

    public MainWindow()
    {
        LoggingService.LogInfo("Initializing MainWindow");
        try
        {
            InitializeComponent();
            LoggingService.LogInfo("MainWindow InitializeComponent completed");
            
            _configService = new ConfigurationService();
            var config = _configService.Load();
            App.AppConfig = config;
            
            string modelPath = config.Model?.Path ?? "";
            string dbPath = config.Database?.VectorStorePath ?? "rag.sqlite";
            
            LoggingService.LogInfo($"Creating MainViewModel with model: {modelPath}, db: {dbPath}");
            var mainVM = new MainViewModel();
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
                urlView.DataContext = new UrlIngestionViewModel(mainVM.WebIngestor, mainVM.SetStatus, this);
                LoggingService.LogInfo("UrlIngestionViewModel setup completed");
            }
            else
            {
                LoggingService.LogInfo("UrlIngestionView control not found, skipping UrlIngestionViewModel setup");
            }

            var settingsView = this.FindControl<UserControl>("SettingsView");
            if (settingsView != null)
            {
                settingsView.DataContext = new SettingsViewModel(_configService);
                LoggingService.LogInfo("SettingsViewModel setup completed");
            }
            else
            {
                LoggingService.LogInfo("SettingsView control not found, skipping SettingsViewModel setup");
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error setting up child ViewModels", ex);
        }
    }
}