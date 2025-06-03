using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using System.Linq;
using DocsDoc.Core.Models;

namespace DocsDoc.RAG
{
    /// <summary>
    /// LlamaSharp-backed implementation of ILlamaSharpInferenceService.
    /// </summary>
    public class LlamaSharpInferenceService : ILlamaSharpInferenceService
    {
        private ModelSettings _modelSettings;
        private LLamaWeights? _model;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private ChatSession? _chatSession;
        private ChatHistory? _chatHistory;
        private bool _disposed;

        public LlamaSharpInferenceService(ModelSettings modelSettings)
        {
            _modelSettings = modelSettings ?? throw new ArgumentNullException(nameof(modelSettings));
            LoggingService.LogInfo($"Initializing LlamaSharpInferenceService with model: {_modelSettings.Path}");
            LoadModel();
        }

        private void LoadModel()
        {
            try
            {
                DisposeResources();
                var modelParams = new ModelParams(_modelSettings.Path!)
                {
                    ContextSize = (uint)_modelSettings.ContextSize,
                    GpuLayerCount = _modelSettings.GpuLayerCount,
                    // Backend selection can be handled here if needed
                };
                _model = LLamaWeights.LoadFromFile(modelParams);
                _context = _model.CreateContext(modelParams);
                _executor = new InteractiveExecutor(_context);
                _chatHistory = new ChatHistory();
                _chatSession = new ChatSession(_executor, _chatHistory);
                LoggingService.LogInfo($"Model loaded: {_modelSettings.Path}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load model: {_modelSettings.Path}", ex);
                throw;
            }
        }

        /// <summary>
        /// Ensures InferenceParams are set with Llama-3 Instruct best practices:
        /// - AntiPrompts: <|eot_id|>
        /// - MaxTokens: 256 (unless explicitly set)
        /// See: https://huggingface.co/MaziyarPanahi/Meta-Llama-3-8B-Instruct-GGUF and https://scisharp.github.io/LLamaSharp/0.24.0/FAQ/
        /// </summary>
        private static InferenceParams EnsureLlama3InferenceParams(object? parameters)
        {
            var infParams = parameters as InferenceParams ?? new InferenceParams();
            if (infParams.AntiPrompts == null || !infParams.AntiPrompts.Any(x => x.Equals("<|eot_id|>", StringComparison.OrdinalIgnoreCase)))
                infParams.AntiPrompts = new List<string> { "<|eot_id|>" };
            if (infParams.MaxTokens == 0)
                infParams.MaxTokens = 256;
            return infParams;
        }

        public async Task<string> GenerateAsync(string prompt, object? parameters = null)
        {
            EnsureNotDisposed();
            if (_executor == null) throw new InvalidOperationException("Model not loaded.");
            var infParams = EnsureLlama3InferenceParams(parameters);
            var result = string.Empty;
            try
            {
                LoggingService.LogInfo($"Generating response for prompt: {prompt}");
                await foreach (var token in _executor.InferAsync(prompt, infParams))
                {
                    result += token;
                }
                LoggingService.LogInfo($"Generation complete for prompt: {prompt}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during generation for prompt: {prompt}", ex);
                throw;
            }
            return result;
        }

        public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, object? parameters = null)
        {
            EnsureNotDisposed();
            if (_executor == null) throw new InvalidOperationException("Model not loaded.");
            var infParams = EnsureLlama3InferenceParams(parameters);
            LoggingService.LogInfo($"Streaming generation for prompt: {prompt}");
            await foreach (var token in _executor.InferAsync(prompt, infParams))
            {
                yield return token;
            }
        }

        /// <summary>
        /// Chat with the model using ChatSession for proper conversational context.
        /// </summary>
        public async Task<string> ChatAsync(string userMessage, object? parameters = null)
        {
            EnsureNotDisposed();
            if (_chatSession == null) throw new InvalidOperationException("Chat session not initialized.");
            var infParams = EnsureLlama3InferenceParams(parameters);
            var result = string.Empty;
            try
            {
                LoggingService.LogInfo($"Processing chat message: {userMessage}");
                await foreach (var token in _chatSession.ChatAsync(
                    new LLama.Common.ChatHistory.Message(LLama.Common.AuthorRole.User, userMessage),
                    infParams))
                {
                    result += token;
                }
                LoggingService.LogInfo($"Chat response generated, length: {result.Length}");
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during chat processing: {userMessage}", ex);
                throw;
            }
        }

        /// <summary>
        /// Add a system message to the chat history.
        /// </summary>
        public void AddSystemMessage(string message)
        {
            if (_chatHistory != null)
            {
                _chatHistory.AddMessage(LLama.Common.AuthorRole.System, message);
                // Rebuild the chat session to ensure the new system message takes effect
                if (_executor != null)
                {
                    _chatSession = new ChatSession(_executor, _chatHistory);
                }
                LoggingService.LogInfo($"Added system message and rebuilt chat session: {message}");
            }
        }

        /// <summary>
        /// Clear chat history and reset to initial system prompt.
        /// </summary>
        public void ClearChatHistory()
        {
            if (_chatHistory != null)
            {
                _chatHistory.Messages.Clear();
                _chatHistory.AddMessage(LLama.Common.AuthorRole.System,
                    "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability.");
                // Rebuild the chat session with the cleared history
                if (_executor != null)
                {
                    _chatSession = new ChatSession(_executor, _chatHistory);
                }
                LoggingService.LogInfo("Chat history cleared, reset, and chat session rebuilt");
            }
        }

        public async Task ReloadModelAsync(ModelSettings newModelSettings)
        {
            _modelSettings = newModelSettings ?? throw new ArgumentNullException(nameof(newModelSettings));
            try
            {
                LoggingService.LogInfo($"Reloading model: {_modelSettings.Path}");
                await Task.Run(() => LoadModel());
                LoggingService.LogInfo($"Model reloaded: {_modelSettings.Path}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to reload model: {_modelSettings.Path}", ex);
                throw;
            }
        }

        /// <summary>
        /// Aggressively reset the chat session and history, fully re-instantiating all objects. Do NOT add a default system message here.
        /// </summary>
        public void FullResetSession()
        {
            if (_executor == null)
                throw new InvalidOperationException("Model not loaded.");
            _chatHistory = new ChatHistory();
            _chatSession = new ChatSession(_executor, _chatHistory);
            LoggingService.LogInfo("Aggressively reset chat session and history (new ChatHistory and ChatSession instantiated, no system message added)");
        }

        /// <summary>
        /// Reload the model and fully reset all state (nuclear reset).
        /// </summary>
        public async Task NuclearResetAsync()
        {
            LoggingService.LogInfo("Performing nuclear model reload (full model/context reset)");
            await ReloadModelAsync(_modelSettings);
            LoggingService.LogInfo("Nuclear model reload complete");
        }

        public async Task ReloadModelAsync(string modelPath, object? parameters = null)
        {
            // Construct a new ModelSettings from the provided path and parameters (ModelParams)
            var modelParams = parameters as ModelParams ?? new ModelParams(modelPath);
            var newSettings = new ModelSettings
            {
                Path = modelPath,
                ContextSize = (int)modelParams.ContextSize,
                GpuLayerCount = modelParams.GpuLayerCount,
                Backend = _modelSettings.Backend,
                EmbeddingModelPath = _modelSettings.EmbeddingModelPath
            };
            await ReloadModelAsync(newSettings);
        }

        private void DisposeResources()
        {
            _chatSession = null;
            _chatHistory = null;
            _context?.Dispose();
            _model?.Dispose();
            _executor = null;
            _context = null;
            _model = null;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LlamaSharpInferenceService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            DisposeResources();
            _disposed = true;
            LoggingService.LogInfo("LlamaSharpInferenceService disposed.");
            GC.SuppressFinalize(this);
        }
    }
} 