using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocsDoc.WebScraper;
using DocsDoc.Core.Services;
using System;
using DocsDoc.Desktop.Views;
using Avalonia.Controls;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the URL ingestion view.
    /// </summary>
    public class UrlIngestionViewModel : INotifyPropertyChanged
    {
        private readonly WebIngestionService _webIngestor;
        private readonly Action<string> _setStatus;
        private readonly Window? _parentWindow;
        public string Url { get => _url; set { _url = value; OnPropertyChanged(nameof(Url)); ((RelayCommand)IngestUrlCommand).RaiseCanExecuteChanged(); } }
        private string _url = string.Empty;
        public string Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }
        private string _progress = string.Empty;
        public ICommand IngestUrlCommand { get; }

        public UrlIngestionViewModel(WebIngestionService webIngestor, Action<string> setStatus, Window? parentWindow = null)
        {
            LoggingService.LogInfo("Initializing UrlIngestionViewModel");
            _webIngestor = webIngestor;
            _setStatus = setStatus;
            _parentWindow = parentWindow;
            IngestUrlCommand = new RelayCommand(async _ => await IngestUrlAsync(), _ => !string.IsNullOrWhiteSpace(Url));
            LoggingService.LogInfo("UrlIngestionViewModel initialized successfully");
        }

        private async Task IngestUrlAsync()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                LoggingService.LogInfo("URL ingestion called with empty URL");
                return;
            }

            var url = Url.Trim();
            LoggingService.LogInfo($"Starting URL ingestion: {url}");

            // Parse base docs URL
            string baseDocsUrl = "";
            try { baseDocsUrl = new Uri(url).GetLeftPart(UriPartial.Authority); } catch { baseDocsUrl = url; }
            string defaultGroupName = GenerateGroupNameFromUrl(url);
            string? groupName = defaultGroupName;
            string entrypointUrl = url;
            if (_parentWindow != null)
            {
                var dialog = new GroupNameDialog(url, baseDocsUrl, defaultGroupName);
                var result = await dialog.ShowDialogAsync(_parentWindow);
                if (result == null)
                {
                    Progress = "Ingestion cancelled: No group name provided.";
                    return;
                }
                entrypointUrl = result.EntrypointUrl;
                baseDocsUrl = result.BaseDocsUrl;
                groupName = result.GroupName;
            }
            if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(entrypointUrl) || string.IsNullOrWhiteSpace(baseDocsUrl))
            {
                Progress = "Ingestion cancelled: Missing required fields.";
                return;
            }

            try
            {
                _setStatus($"Ingesting {entrypointUrl}...");
                Progress = "Starting ingestion...";

                await _webIngestor.IngestUrlAsync(entrypointUrl, groupName: groupName, baseDocsUrl: baseDocsUrl, progress: new Progress<string>(message =>
                {
                    LoggingService.LogInfo($"URL ingestion progress: {message}");
                    Progress = message;
                }));

                _setStatus($"Done: {entrypointUrl}");
                Progress = $"Completed: {entrypointUrl}";
                LoggingService.LogInfo($"URL ingestion completed successfully: {entrypointUrl}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"URL ingestion failed: {entrypointUrl}", ex);
                var errorMsg = $"Error: {ex.Message}";
                _setStatus(errorMsg);
                Progress = errorMsg;
            }
        }

        private string GenerateGroupNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host.Replace("www.", "");
                var parts = host.Split('.');
                if (parts.Length >= 2)
                    return $"{Capitalize(parts[parts.Length - 2])} Docs";
                return host;
            }
            catch { return "Web Docs"; }
        }
        private string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 