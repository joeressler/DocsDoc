using System.Collections.Generic;
using System.Threading.Tasks;
using DocsDoc.Core.Models;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Provides LlamaSharp model loading, inference, and chat APIs.
    /// </summary>
    public interface ILlamaSharpInferenceService : System.IDisposable
    {
        /// <summary>
        /// Generate a response for a prompt (non-streaming).
        /// <param name="parameters">Should be LLama.Common.InferenceParams if available.</param>
        /// </summary>
        Task<string> GenerateAsync(string prompt, object? parameters = null);

        /// <summary>
        /// Generate a response for a prompt (streaming).
        /// <param name="parameters">Should be LLama.Common.InferenceParams if available.</param>
        /// </summary>
        IAsyncEnumerable<string> GenerateStreamAsync(string prompt, object? parameters = null);

        /// <summary>
        /// Chat with the model using proper conversational context.
        /// <param name="parameters">Should be LLama.Common.InferenceParams if available.</param>
        /// </summary>
        Task<string> ChatAsync(string userMessage, object? parameters = null);

        /// <summary>
        /// Add a system message to the chat history.
        /// </summary>
        void AddSystemMessage(string message);

        /// <summary>
        /// Clear chat history and reset to initial system prompt.
        /// </summary>
        void ClearChatHistory();

        /// <summary>
        /// Reload the model from a new path and/or parameters.
        /// <param name="parameters">Should be LLama.Common.ModelParams if available.</param>
        /// </summary>
        Task ReloadModelAsync(string modelPath, object? parameters = null);

        /// <summary>
        /// Reload the model from a new ModelSettings object.
        /// </summary>
        Task ReloadModelAsync(ModelSettings newModelSettings);

        /// <summary>
        /// Aggressively reset the chat session and history, fully re-instantiating all objects.
        /// </summary>
        void FullResetSession();

        /// <summary>
        /// Reload the model and fully reset all state (nuclear reset).
        /// </summary>
        Task NuclearResetAsync();
    }
} 