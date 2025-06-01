using System.Collections.Generic;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Splits text into chunks for embedding.
    /// </summary>
    public interface ITextChunker
    {
        /// <summary>
        /// Splits text into chunks.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <param name="chunkSize">Chunk size (words or tokens).</param>
        /// <param name="overlap">Number of words/tokens to overlap.</param>
        /// <returns>List of text chunks.</returns>
        IReadOnlyList<string> ChunkText(string text, int chunkSize, int overlap = 0);
    }
} 