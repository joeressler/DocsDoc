using DocsDoc.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DocsDoc.RAG.Processing
{
    /// <summary>
    /// Default implementation for splitting text into chunks for embedding.
    /// </summary>
    public class DefaultTextChunker : ITextChunker
    {
        public IReadOnlyList<string> ChunkText(string text, int chunkSize, int overlap = 0)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (overlap < 0) throw new ArgumentOutOfRangeException(nameof(overlap));
            var words = Regex.Split(text, "\\s+");
            var chunks = new List<string>();
            for (int i = 0; i < words.Length; i += chunkSize - overlap)
            {
                int end = Math.Min(i + chunkSize, words.Length);
                var chunk = string.Join(" ", words, i, end - i);
                if (!string.IsNullOrWhiteSpace(chunk))
                    chunks.Add(chunk.Trim());
                if (end == words.Length) break;
            }
            return chunks;
        }
    }
} 