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

        public MainViewModel(string modelPath, string dbPath)
        {
            LoggingService.LogInfo($"Initializing MainViewModel with model: {modelPath}, db: {dbPath}");
            try
            {
                Orchestrator = new RagOrchestrator(modelPath, dbPath);
                WebIngestor = Orchestrator.GetWebIngestionService();
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
                
                // Add documents to UI
                foreach (var source in documentSources)
                {
                    var docInfo = new DocumentInfo { Name = source, Path = source };
                    Documents.Add(docInfo);
                }
                
                SetStatus($"Loaded {documentSources.Count} existing documents");
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
                var fileName = System.IO.Path.GetFileName(filePath);
                var docInfo = new DocumentInfo { Name = fileName, Path = filePath };
                
                // Check if document already exists in list (prevent duplicates)
                bool exists = false;
                foreach (var existingDoc in Documents)
                {
                    if (existingDoc.Name == fileName)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    Documents.Add(docInfo);
                }
                
                SetStatus($"Ingested document: {fileName}");
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
                SetStatus($"Removing document: {doc.Name}");
                
                // Remove from vector store using the document name (which is the source)
                await Orchestrator.VectorStore.DeleteDocumentAsync(doc.Name);
                LoggingService.LogInfo($"Deleted document from vector store: {doc.Name}");
                
                // Remove from UI
                Documents.Remove(doc);
                SetStatus($"Removed document: {doc.Name}");
                LoggingService.LogInfo($"Document removal completed successfully: {doc.Name}");
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
            return Documents.Where(d => d.IsSelectedForRag).Select(d => d.Name);
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
    }
} 