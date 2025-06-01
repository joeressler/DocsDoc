using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocsDoc.RAG;
using DocsDoc.Core.Services;
using DocsDoc.Core.Interfaces;
using System;
using System.Linq;
using System.Collections.Generic;

namespace DocsDoc.Desktop.ViewModels
{
    /// <summary>
    /// ViewModel for the chat view with conversational RAG support.
    /// </summary>
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly RagOrchestrator _orchestrator;
        private readonly ILlamaSharpInferenceService _llmService;
        private readonly IRetrievalEngine _retriever;
        private readonly IContextAugmenter _contextAugmenter;
        private readonly Action<string> _setStatus;
        private readonly MainViewModel _mainViewModel;
        private List<string> _lastSelectedSources = new();
        private bool _conversationStarted = false;
        
        public ObservableCollection<ChatMessage> ChatHistory { get; } = new();
        public string UserInput 
        { 
            get => _userInput ?? string.Empty; 
            set 
            { 
                _userInput = value; 
                OnPropertyChanged(nameof(UserInput));
                ((RelayCommand)SendMessageCommand).RaiseCanExecuteChanged();
            } 
        }
        private string? _userInput;
        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }

        /// <summary>
        /// Display text showing which document sources are currently active for RAG.
        /// </summary>
        public string ActiveSourcesText
        {
            get
            {
                var selectedSources = _mainViewModel.GetSelectedDocumentSources().ToList();
                if (!selectedSources.Any())
                    return "No document sources selected - chat will use general knowledge only.";
                
                return $"Active document sources for RAG: {string.Join(", ", selectedSources)}";
            }
        }

        public ChatViewModel(RagOrchestrator orchestrator, Action<string> setStatus, MainViewModel mainViewModel)
        {
            LoggingService.LogInfo("Initializing ChatViewModel");
            _orchestrator = orchestrator;
            _llmService = orchestrator.LlmService;
            
            // Create a retrieval engine that uses the orchestrator's components
            var embeddingService = new DocsDoc.RAG.Embedding.LlamaSharpEmbeddingService(_llmService);
            _retriever = new DocsDoc.RAG.Retrieval.DefaultRetrievalEngine(embeddingService, _orchestrator.VectorStore);
            _contextAugmenter = new DocsDoc.RAG.Retrieval.DefaultContextAugmenter();
            
            _setStatus = setStatus;
            _mainViewModel = mainViewModel;
            SendMessageCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(UserInput));
            ClearChatCommand = new RelayCommand(async _ => await ClearChatAsync(), _ => ChatHistory.Count > 0);
            
            // Subscribe to document changes to update active sources display
            _mainViewModel.Documents.CollectionChanged += (_, __) => OnPropertyChanged(nameof(ActiveSourcesText));
            DocumentInfo.RagSelectionChanged += (_, __) => {
                RefreshActiveSourcesDisplay();
                CheckAndResetConversationIfSourcesChanged();
            };
            
            LoggingService.LogInfo("ChatViewModel initialized successfully");
        }

        /// <summary>
        /// Check if document sources have changed and reset conversation if needed.
        /// </summary>
        private void CheckAndResetConversationIfSourcesChanged()
        {
            var currentSources = _mainViewModel.GetSelectedDocumentSources().OrderBy(s => s).ToList();
            var lastSources = _lastSelectedSources.OrderBy(s => s).ToList();
            
            LoggingService.LogInfo($"Checking source changes: Current=[{string.Join(", ", currentSources)}], Last=[{string.Join(", ", lastSources)}]");
            
            // Compare source lists
            bool sourcesChanged = !currentSources.SequenceEqual(lastSources);
            
            if (sourcesChanged)
            {
                LoggingService.LogInfo($"Document sources changed from [{string.Join(", ", lastSources)}] to [{string.Join(", ", currentSources)}] - resetting conversation completely");
                
                // Force complete reset regardless of conversation state
                ResetConversationContext();
                
                // Add a system message to indicate the context change
                ChatHistory.Add(new ChatMessage 
                { 
                    Role = "System", 
                    Text = $"📝 Context changed - now using sources: {(currentSources.Any() ? string.Join(", ", currentSources) : "general knowledge only")}"
                });
                
                LoggingService.LogInfo("Source change handled - conversation will be re-initialized on next message");
            }
            else
            {
                LoggingService.LogInfo("No source changes detected");
            }
            
            _lastSelectedSources = currentSources;
        }

        /// <summary>
        /// Reset the LLM conversation context without clearing the UI chat history.
        /// </summary>
        private void ResetConversationContext()
        {
            LoggingService.LogInfo("Aggressively resetting conversation context completely");
            _llmService.FullResetSession();
            _conversationStarted = false;
            LoggingService.LogInfo("Conversation context reset - _conversationStarted set to false");
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput))
            {
                LoggingService.LogInfo("Send message called with empty input");
                return;
            }

            var userMessage = UserInput.Trim();
            LoggingService.LogInfo($"Sending user message: {userMessage}");
            
            try
            {
                var userMsg = new ChatMessage { Role = "User", Text = userMessage };
                ChatHistory.Add(userMsg);
                LoggingService.LogInfo("User message added to chat history");
                
                // Get selected document sources for RAG filtering
                var selectedSources = _mainViewModel.GetSelectedDocumentSources().ToList();
                LoggingService.LogInfo($"Current conversation state: _conversationStarted={_conversationStarted}");
                LoggingService.LogInfo($"Selected document sources: [{string.Join(", ", selectedSources)}] (Count: {selectedSources.Count})");
                
                // If this is the first message or sources changed, set up the conversation context
                if (!_conversationStarted)
                {
                    LoggingService.LogInfo("Setting up new conversation context");
                    IReadOnlyList<string> contextChunks;
                    
                    if (selectedSources.Any())
                    {
                        _setStatus("Retrieving relevant context...");
                        // Only retrieve context if sources are actually selected
                        contextChunks = await _retriever.RetrieveRelevantChunksAsync(userMessage, 5, selectedSources);
                        LoggingService.LogInfo($"Retrieved {contextChunks.Count} context chunks for conversation setup from selected sources: [{string.Join(", ", selectedSources)}]");
                    }
                    else
                    {
                        // No sources selected - use general knowledge only (no RAG context)
                        contextChunks = new List<string>();
                        LoggingService.LogInfo("No document sources selected - using general knowledge only (no RAG context)");
                    }
                    
                    await SetupConversationContext(contextChunks, selectedSources);
                    _conversationStarted = true;
                    LoggingService.LogInfo("Conversation context setup completed - _conversationStarted set to true");
                }
                else
                {
                    LoggingService.LogInfo("Continuing existing conversation (no context setup needed)");
                }
                
                _setStatus("Generating response...");
                LoggingService.LogInfo("Starting conversational RAG generation");
                
                // Use the LLM's chat capability for conversational context with proper Llama-3 parameters
                var inferenceParams = new { MaxTokens = 256, AntiPrompts = new string[] { "<|eot_id|>" } };
                var response = await _llmService.ChatAsync(userMessage, inferenceParams);
                LoggingService.LogInfo($"Conversational RAG response generated, length: {response?.Length ?? 0}");
                
                // Add assistant response to UI
                var assistantMsg = new ChatMessage { Role = "Assistant", Text = response ?? "No response generated" };
                ChatHistory.Add(assistantMsg);
                
                _setStatus("Response received.");
                LoggingService.LogInfo("Assistant response added to chat history");
                
                UserInput = string.Empty;
                LoggingService.LogInfo("User input cleared");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during conversational RAG message processing: {userMessage}", ex);
                ChatHistory.Add(new ChatMessage { Role = "Assistant", Text = $"Error: {ex.Message}" });
                _setStatus($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Set up the conversation context with RAG information by completely resetting the chat session.
        /// </summary>
        private async Task SetupConversationContext(IReadOnlyList<string> contextChunks, List<string> selectedSources)
        {
            LoggingService.LogInfo("Setting up conversation context with RAG information");
            
            // Clear and rebuild chat history with proper context
            _llmService.ClearChatHistory();
            
            // Only try fallback retrieval if we have sources selected but no context found
            // DO NOT retrieve context if no sources are selected at all
            if (!contextChunks.Any() && selectedSources.Any())
            {
                LoggingService.LogInfo("No context found for current query, retrieving general context from selected sources");
                // Use a generic query to get some representative content from the selected sources
                contextChunks = await _retriever.RetrieveRelevantChunksAsync("introduction overview main topics", 3, selectedSources);
                LoggingService.LogInfo($"Retrieved {contextChunks.Count} general context chunks from selected sources");
            }
            
            // Build context information for the system message
            var contextInfo = contextChunks.Any() 
                ? $"\n\nYou have access to the following relevant information from the selected documents ({string.Join(", ", selectedSources)}):\n\n" +
                  string.Join("\n---\n", contextChunks) + 
                  "\n\nUse this information to answer questions when relevant, but you can also use your general knowledge for topics not covered in the provided context."
                : selectedSources.Any() 
                    ? $"\n\nNo specific context was found in the selected documents ({string.Join(", ", selectedSources)}) for this conversation, so rely on your general knowledge."
                    : "\n\nNo document sources are selected, so use your general knowledge to assist the user.";

            var systemMessage = "You are a helpful, smart, kind, and efficient AI assistant. " +
                               "You always fulfill the user's requests to the best of your ability." + 
                               contextInfo;

            _llmService.AddSystemMessage(systemMessage);
            LoggingService.LogInfo($"Conversation context setup completed with {contextChunks.Count} context chunks");
        }

        private Task ClearChatAsync()
        {
            LoggingService.LogInfo("Clearing chat history and conversation context");
            try
            {
                _setStatus("Clearing chat...");
                
                // Reset both UI and LLM conversation state
                ResetConversationContext();
                ChatHistory.Clear();
                _lastSelectedSources.Clear();
                
                _setStatus("Chat cleared.");
                LoggingService.LogInfo("Chat history and conversation context cleared successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error clearing chat history", ex);
                _setStatus($"Error clearing chat: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Refresh the active sources display - call this when document selections change.
        /// </summary>
        public void RefreshActiveSourcesDisplay()
        {
            OnPropertyChanged(nameof(ActiveSourcesText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 