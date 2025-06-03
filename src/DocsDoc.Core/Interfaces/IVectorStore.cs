using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Stores and searches vector embeddings for RAG.
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Add embeddings, texts, and IDs to the store.
        /// </summary>
        Task AddAsync(IEnumerable<float[]> embeddings, IEnumerable<string> texts, IEnumerable<string> ids);

        /// <summary>
        /// Search for top-K most similar embeddings. Returns (id, score) pairs.
        /// </summary>
        /// <param name="queryEmbedding">Query embedding vector</param>
        /// <param name="topK">Number of top results to return</param>
        /// <param name="documentSources">Optional filter to limit search to specific document sources</param>
        Task<IReadOnlyList<(string id, float score)>> SearchAsync(float[] queryEmbedding, int topK, IEnumerable<string>? documentSources = null);

        /// <summary>
        /// Retrieve original text by ID.
        /// </summary>
        Task<string?> GetTextByIdAsync(string id);

        /// <summary>
        /// Get all unique document sources currently stored in the vector database.
        /// </summary>
        Task<IReadOnlyList<string>> GetAllDocumentSourcesAsync();

        /// <summary>
        /// Delete all embeddings and chunks associated with a specific document source.
        /// </summary>
        Task DeleteDocumentAsync(string documentSource);

        /// <summary>
        /// Delete all embeddings and chunks associated with a document group (by group name prefix).
        /// </summary>
        Task DeleteDocumentGroupAsync(string groupName);
    }
} 