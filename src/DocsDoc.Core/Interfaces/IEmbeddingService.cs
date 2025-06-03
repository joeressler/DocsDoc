using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Provides embedding-only functionality for text chunks. Not for LLMs.
    /// </summary>
    public interface IEmbeddingProvider
    {
        /// <summary>
        /// Generate embeddings for a list of text chunks.
        /// </summary>
        /// <param name="texts">Text chunks to embed.</param>
        /// <returns>List of embeddings (one per chunk).</returns>
        Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> texts);
    }
} 