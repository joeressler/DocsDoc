using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocsDoc.WebScraper;
using DocsDoc.Core.Services;
using System;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the URL ingestion view.
    /// </summary>
    public class UrlIngestionViewModel : INotifyPropertyChanged
    {
        private readonly WebIngestionService _webIngestor;
        private readonly Action<string> _setStatus;
        public string Url { get => _url; set { _url = value; OnPropertyChanged(nameof(Url)); } }
        private string _url = string.Empty;
        public string Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }
        private string _progress = string.Empty;
        public ICommand IngestUrlCommand { get; }

        public UrlIngestionViewModel(WebIngestionService webIngestor, Action<string> setStatus)
        {
            LoggingService.LogInfo("Initializing UrlIngestionViewModel");
            _webIngestor = webIngestor;
            _setStatus = setStatus;
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
            
            try
            {
                _setStatus($"Ingesting {url}...");
                Progress = "Starting ingestion...";
                
                await _webIngestor.IngestUrlAsync(url, progress: message => 
                {
                    LoggingService.LogInfo($"URL ingestion progress: {message}");
                    Progress = message;
                });
                
                _setStatus($"Done: {url}");
                Progress = $"Completed: {url}";
                LoggingService.LogInfo($"URL ingestion completed successfully: {url}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"URL ingestion failed: {url}", ex);
                var errorMsg = $"Error: {ex.Message}";
                _setStatus(errorMsg);
                Progress = errorMsg;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 