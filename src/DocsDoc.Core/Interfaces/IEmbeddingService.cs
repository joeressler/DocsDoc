using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Generates vector embeddings for text chunks.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generate embeddings for a list of text chunks.
        /// </summary>
        /// <param name="texts">Text chunks to embed.</param>
        /// <returns>List of embeddings (one per chunk).</returns>
        Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> texts);
    }
} 