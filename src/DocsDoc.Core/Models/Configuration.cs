using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DocsDoc.Core.Models
{
    /// <summary>
    /// Base class for settings models that supports property change notifications.
    /// </summary>
    public abstract class SettingsBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Represents application configuration settings.
    /// </summary>
    public class Configuration : SettingsBase
    {
        private ModelSettings? _model;
        public ModelSettings? Model
        {
            get => _model;
            set => SetField(ref _model, value);
        }

        private DatabaseSettings? _database;
        public DatabaseSettings? Database
        {
            get => _database;
            set => SetField(ref _database, value);
        }

        private RagSettings? _rag;
        public RagSettings? RAG // Changed from Rag to RAG to match appsettings.json
        {
            get => _rag;
            set => SetField(ref _rag, value);
        }

        private WebScraperSettings? _webScraper;
        public WebScraperSettings? WebScraper
        {
            get => _webScraper;
            set => SetField(ref _webScraper, value);
        }

        private LoggingSettings? _logging;
        public LoggingSettings? Logging
        {
            get => _logging;
            set => SetField(ref _logging, value);
        }

        private UISettings? _ui;
        public UISettings? UI // Changed from Ui to UI to match appsettings.json
        {
            get => _ui;
            set => SetField(ref _ui, value);
        }

        public Configuration()
        {
            Model = new ModelSettings();
            Database = new DatabaseSettings();
            RAG = new RagSettings();
            WebScraper = new WebScraperSettings();
            Logging = new LoggingSettings();
            UI = new UISettings();
        }
    }

    public class ModelSettings : SettingsBase
    {
        private string? _path;
        public string? Path { get => _path; set => SetField(ref _path, value); }

        private string? _backend;
        public string? Backend { get => _backend; set => SetField(ref _backend, value); }

        private int _contextSize = 1024;
        public int ContextSize { get => _contextSize; set => SetField(ref _contextSize, value); }

        private int _gpuLayerCount = 0;
        public int GpuLayerCount { get => _gpuLayerCount; set => SetField(ref _gpuLayerCount, value); }

        private string? _embeddingModelPath;
        public string? EmbeddingModelPath { get => _embeddingModelPath; set => SetField(ref _embeddingModelPath, value); }
    }

    public class DatabaseSettings : SettingsBase
    {
        private string? _vectorStorePath;
        public string? VectorStorePath { get => _vectorStorePath; set => SetField(ref _vectorStorePath, value); }
    }

    public class RagSettings : SettingsBase
    {
        private int _chunkSize = 512;
        public int ChunkSize { get => _chunkSize; set => SetField(ref _chunkSize, value); }

        private int _chunkOverlap = 64;
        public int ChunkOverlap { get => _chunkOverlap; set => SetField(ref _chunkOverlap, value); }

        private int _retrievalTopK = 5;
        public int RetrievalTopK { get => _retrievalTopK; set => SetField(ref _retrievalTopK, value); }
    }

    public class WebScraperSettings : SettingsBase
    {
        private string? _userAgent = "DocsDocBot/1.0";
        public string? UserAgent { get => _userAgent; set => SetField(ref _userAgent, value); }

        private int _rateLimitSeconds = 2;
        public int RateLimitSeconds { get => _rateLimitSeconds; set => SetField(ref _rateLimitSeconds, value); }

        private int _maxConcurrentRequests = 2;
        public int MaxConcurrentRequests { get => _maxConcurrentRequests; set => SetField(ref _maxConcurrentRequests, value); }

        private int _maxCrawlDepth = 5;
        public int MaxCrawlDepth { get => _maxCrawlDepth; set => SetField(ref _maxCrawlDepth, value); }

        private List<string>? _allowedDomains = new List<string>();
        public List<string>? AllowedDomains { get => _allowedDomains; set => SetField(ref _allowedDomains, value); }

        private string? _cachePath;
        public string? CachePath { get => _cachePath; set => SetField(ref _cachePath, value); }
    }

    public class LoggingSettings : SettingsBase
    {
        private LogLevelSettings? _logLevel;
        public LogLevelSettings? LogLevel 
        { 
            get => _logLevel; 
            set => SetField(ref _logLevel, value); 
        }

        private string? _logFilePath;
        public string? LogFilePath { get => _logFilePath; set => SetField(ref _logFilePath, value); }
        
        public LoggingSettings()
        {
            LogLevel = new LogLevelSettings();
        }
    }

    public class LogLevelSettings : SettingsBase
    {
        private string? _default = "Information";
        public string? Default { get => _default; set => SetField(ref _default, value); }

        private string? _microsoft = "Warning";
        public string? Microsoft { get => _microsoft; set => SetField(ref _microsoft, value); }

        private string? _microsoftHostingLifetime = "Information";
        public string? MicrosoftHostingLifetime { get => _microsoftHostingLifetime; set => SetField(ref _microsoftHostingLifetime, value); }
    }

    public class UISettings : SettingsBase
    {
        private string? _theme = "Light";
        public string? Theme { get => _theme; set => SetField(ref _theme, value); }
    }
} 