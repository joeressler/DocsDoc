using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using System.Linq;

namespace DocsDoc.RAG
{
    /// <summary>
    /// LlamaSharp-backed implementation of ILlamaSharpInferenceService.
    /// </summary>
    public class LlamaSharpInferenceService : ILlamaSharpInferenceService
    {
        private LLamaWeights? _model;
        private LLamaContext? _context;
        private InteractiveExecutor? _executor;
        private ChatSession? _chatSession;
        private ChatHistory? _chatHistory;
        private string? _modelPath;
        private ModelParams? _modelParams;
        private bool _disposed;

        public LlamaSharpInferenceService(string modelPath, ModelParams? modelParams = null)
        {
            _modelPath = modelPath;
            _modelParams = modelParams ?? new ModelParams(_modelPath);
            LoggingService.LogInfo($"Initializing LlamaSharpInferenceService with model: {_modelPath}");
            LoadModel();
        }

        private void LoadModel()
        {
            try
            {
                DisposeResources();
                _model = LLamaWeights.LoadFromFile(_modelParams!);
                _context = _model.CreateContext(_modelParams!);
                _executor = new InteractiveExecutor(_context);
                
                // Initialize chat session with system prompt
                _chatHistory = new ChatHistory();
                _chatHistory.AddMessage(AuthorRole.System, 
                    "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability.");
                _chatSession = new ChatSession(_executor, _chatHistory);
                
                LoggingService.LogInfo($"Model loaded: {_modelPath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load model: {_modelPath}", ex);
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
                    new ChatHistory.Message(AuthorRole.User, userMessage), 
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
                _chatHistory.AddMessage(AuthorRole.System, message);
                
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
                _chatHistory.AddMessage(AuthorRole.System, 
                    "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability.");
                
                // Rebuild the chat session with the cleared history
                if (_executor != null)
                {
                    _chatSession = new ChatSession(_executor, _chatHistory);
                }
                
                LoggingService.LogInfo("Chat history cleared, reset, and chat session rebuilt");
            }
        }

        public async Task<IReadOnlyList<float[]>> GetEmbeddingAsync(string text)
        {
            EnsureNotDisposed();
            if (_context == null) throw new InvalidOperationException("Model not loaded.");
            try
            {
                LoggingService.LogInfo($"Generating embedding for text: {text}");
                var embedder = new LLamaEmbedder(_model!, _context.Params, null);
                var result = await Task.Run(() => embedder.GetEmbeddings(text));
                LoggingService.LogInfo($"Embedding generated for text: {text}");
                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during embedding for text: {text}", ex);
                throw;
            }
        }

        public async Task ReloadModelAsync(string modelPath, object? parameters = null)
        {
            _modelPath = modelPath;
            _modelParams = parameters as ModelParams ?? new ModelParams(modelPath);
            try
            {
                LoggingService.LogInfo($"Reloading model: {_modelPath}");
                await Task.Run(() => LoadModel());
                LoggingService.LogInfo($"Model reloaded: {_modelPath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to reload model: {_modelPath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Aggressively reset the chat session and history, fully re-instantiating all objects.
        /// </summary>
        public void FullResetSession()
        {
            if (_executor == null)
                throw new InvalidOperationException("Model not loaded.");
            _chatHistory = new ChatHistory();
            _chatHistory.AddMessage(AuthorRole.System, "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability.");
            _chatSession = new ChatSession(_executor, _chatHistory);
            LoggingService.LogInfo("Aggressively reset chat session and history (new ChatHistory and ChatSession instantiated)");
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