using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using DocsDoc.RAG;
using DocsDoc.WebScraper;
using DocsDoc.Desktop.ViewModels;
using System.Windows.Input;
using DocsDoc.Core.Services;
using System.Linq;
using System.Collections.Generic;
using DocsDoc.WebScraper.Analysis;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// Main view model for DocsDoc Desktop. Holds orchestrator, web ingestion, and navigation.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        public RagOrchestrator Orchestrator { get; }
        public WebIngestionService WebIngestor { get; }
        public ObservableCollection<ChatMessage> ChatHistory { get; } = new();
        public ObservableCollection<DocumentInfo> Documents { get; } = new();
        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        private string _status = "Ready";
        public ICommand IngestDocumentCommand { get; }
        public ICommand RemoveDocumentCommand { get; }
        public ICommand ReIngestDocumentCommand { get; }
        public ICommand SelectAllForRagCommand { get; }
        public ICommand DeselectAllForRagCommand { get; }
        private DocumentInfo? _selectedDocument;
        public DocumentInfo? SelectedDocument
        {
            get => _selectedDocument;
            set { _selectedDocument = value; OnPropertyChanged(nameof(SelectedDocument)); }
        }

        public MainViewModel()
        {
            // Use App.AppConfig for paths
            var modelSettings = App.AppConfig?.Model;
            var databaseSettings = App.AppConfig?.Database;
            var ragSettings = App.AppConfig?.RAG;
            var dbPath = App.AppConfig?.Database?.VectorStorePath ?? "rag.sqlite";
            var ragChunkSize = ragSettings?.ChunkSize ?? 512;
            var ragChunkOverlap = ragSettings?.ChunkOverlap ?? 64;
            var ragRetrievalTopK = ragSettings?.RetrievalTopK ?? 5;

            var webScraperSettings = App.AppConfig?.WebScraper;

            LoggingService.LogInfo($"Initializing MainViewModel with model: {modelSettings?.Path}, db: {databaseSettings?.VectorStorePath}");
            try
            {
                Orchestrator = new RagOrchestrator(modelSettings, databaseSettings, ragSettings, ragChunkSize, ragChunkOverlap, ragRetrievalTopK);
                WebIngestor = Orchestrator.GetWebIngestionService(webScraperSettings, ragSettings);
                IngestDocumentCommand = new RelayCommand(async path => await IngestDocumentAsync(path as string), path => path is string s && !string.IsNullOrWhiteSpace(s));
                RemoveDocumentCommand = new RelayCommand(async doc => await RemoveDocumentAsync(doc as DocumentInfo), doc => doc is DocumentInfo);
                ReIngestDocumentCommand = new RelayCommand(async doc => await ReIngestDocumentAsync(doc as DocumentInfo), doc => doc is DocumentInfo);
                SelectAllForRagCommand = new RelayCommand(async _ => await SelectAllForRagAsync(), _ => Documents.Any());
                DeselectAllForRagCommand = new RelayCommand(async _ => await DeselectAllForRagAsync(), _ => Documents.Any());
                
                // Load existing documents from vector store
                _ = Task.Run(LoadExistingDocumentsAsync);
                
                LoggingService.LogInfo("MainViewModel initialized successfully.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error initializing MainViewModel", ex);
                throw;
            }
        }

        // TODO: Add commands for chat, doc ingest, url ingest, etc.
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Set the status message for the UI (updates StatusBar).
        /// </summary>
        public void SetStatus(string message)
        {
            LoggingService.LogInfo($"Status update: {message}");
            Status = message;
        }

        /// <summary>
        /// Load existing documents from the vector store on startup.
        /// </summary>
        private async Task LoadExistingDocumentsAsync()
        {
            try
            {
                LoggingService.LogInfo("Loading existing documents from vector store");
                SetStatus("Loading existing documents...");
                var documentSources = await Orchestrator.VectorStore.GetAllDocumentSourcesAsync();
                LoggingService.LogInfo($"Found {documentSources.Count} existing documents in vector store");

                // Group by groupName only
                var groupDict = new Dictionary<string, DocumentInfo>();
                foreach (var source in documentSources)
                {
                    string groupName;
                    string pageUrl;
                    if (source.Contains("|"))
                    {
                        var parts = source.Split('|');
                        groupName = parts[0];
                        if (parts.Length > 2)
                            pageUrl = parts[2];
                        else if (parts.Length > 1)
                            pageUrl = parts[1];
                        else
                            pageUrl = source;
                    }
                    else
                    {
                        groupName = System.IO.Path.GetFileNameWithoutExtension(source);
                        pageUrl = source;
                    }
                    if (!groupDict.TryGetValue(groupName, out var docInfo))
                    {
                        docInfo = new DocumentInfo
                        {
                            Name = groupName,
                            Path = source, // Could use first source as path
                            AllSources = new List<string>(),
                            Pages = new List<PageInfo>()
                        };
                        groupDict[groupName] = docInfo;
                    }
                    docInfo.AllSources.Add(source);
                    docInfo.Pages.Add(new PageInfo { Url = pageUrl });
                }
                // Add to Documents collection
                foreach (var doc in groupDict.Values)
                {
                    Documents.Add(doc);
                }
                SetStatus($"Loaded {groupDict.Count} document groups");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error loading existing documents", ex);
                SetStatus("Error loading existing documents");
            }
        }

        /// <summary>
        /// Ingest a document file, update Documents and Status.
        /// </summary>
        public async Task IngestDocumentAsync(string filePath)
        {
            LoggingService.LogInfo($"Starting document ingestion: {filePath}");
            try
            {
                SetStatus($"Ingesting document: {filePath}");
                await Orchestrator.IngestDocumentAsync(filePath);
                // Determine groupName for file
                string groupName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                // Check if group already exists
                var existingDoc = Documents.FirstOrDefault(d => d.Name == groupName);
                if (existingDoc != null)
                {
                    // Add new source/page to existing group
                    if (!existingDoc.AllSources.Contains(filePath))
                    {
                        existingDoc.AllSources.Add(filePath);
                        existingDoc.Pages.Add(new PageInfo { Url = filePath });
                    }
                }
                else
                {
                    var docInfo = new DocumentInfo
                    {
                        Name = groupName,
                        Path = filePath,
                        AllSources = new List<string> { filePath },
                        Pages = new List<PageInfo> { new PageInfo { Url = filePath } }
                    };
                    Documents.Add(docInfo);
                }
                SetStatus($"Ingested document: {groupName}");
                LoggingService.LogInfo($"Document ingestion completed successfully: {filePath}");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error ingesting {filePath}: {ex.Message}";
                LoggingService.LogError($"Document ingestion failed: {filePath}", ex);
                SetStatus(errorMsg);
            }
        }

        /// <summary>
        /// Remove a document from the UI and vector store.
        /// </summary>
        public async Task RemoveDocumentAsync(DocumentInfo doc)
        {
            if (doc == null)
            {
                LoggingService.LogInfo("Remove document called with null document");
                return;
            }
            LoggingService.LogInfo($"Starting document removal: {doc.Name}");
            try
            {
                SetStatus($"Removing document group: {doc.Name}");
                // Remove from vector store using the group name (removes all sources for the group)
                await Orchestrator.VectorStore.DeleteDocumentGroupAsync(doc.Name);
                LoggingService.LogInfo($"Deleted document group from vector store: {doc.Name}");
                // Remove from UI
                Documents.Remove(doc);
                SetStatus($"Removed document group: {doc.Name}");
                LoggingService.LogInfo($"Document group removal completed successfully: {doc.Name}");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error removing {doc.Name}: {ex.Message}";
                LoggingService.LogError($"Document removal failed: {doc.Name}", ex);
                SetStatus(errorMsg);
            }
        }

        /// <summary>
        /// Re-ingest a document (re-run pipeline for the file).
        /// </summary>
        public async Task ReIngestDocumentAsync(DocumentInfo doc)
        {
            if (doc == null)
            {
                LoggingService.LogInfo("Re-ingest document called with null document");
                return;
            }
            
            LoggingService.LogInfo($"Starting document re-ingestion: {doc.Name}");
            
            // First remove the existing document from vector store
            await RemoveDocumentAsync(doc);
            
            // Then re-ingest it
            await IngestDocumentAsync(doc.Path);
        }

        /// <summary>
        /// Get the list of document sources that are currently selected for RAG queries.
        /// </summary>
        public IEnumerable<string> GetSelectedDocumentSources()
        {
            // Return all actual document sources for selected groups
            return Documents.Where(d => d.IsSelectedForRag)
                            .SelectMany(d => d.AllSources)
                            .Distinct();
        }

        /// <summary>
        /// Select all documents for RAG queries.
        /// </summary>
        public async Task SelectAllForRagAsync()
        {
            await Task.Run(() =>
            {
                foreach (var doc in Documents)
                {
                    doc.IsSelectedForRag = true;
                }
            });
            LoggingService.LogInfo("Selected all documents for RAG");
        }

        /// <summary>
        /// Deselect all documents for RAG queries.
        /// </summary>
        public async Task DeselectAllForRagAsync()
        {
            await Task.Run(() =>
            {
                foreach (var doc in Documents)
                {
                    doc.IsSelectedForRag = false;
                }
            });
            LoggingService.LogInfo("Deselected all documents for RAG");
        }

        /// <summary>
        /// Export the current site graph (from cache) to DOT and JSON files.
        /// </summary>
        public void ExportSiteGraph()
        {
            try
            {
                var cacheDir = App.AppConfig?.WebScraper?.CachePath ?? "cache/pages";
                var parser = new SiteMapParser();
                var graph = parser.BuildGraphFromCache(cacheDir);
                parser.ExportToDot(graph, System.IO.Path.Combine(cacheDir, "sitemap.dot"));
                parser.ExportToJson(graph, System.IO.Path.Combine(cacheDir, "sitemap.json"));
                SetStatus("Exported site graph to DOT and JSON.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error exporting site graph: {ex.Message}");
            }
        }
    }
} 