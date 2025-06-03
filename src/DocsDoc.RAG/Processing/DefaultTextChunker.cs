using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Models;
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
        private readonly RagSettings _ragSettings;
        public DefaultTextChunker(RagSettings ragSettings)
        {
            _ragSettings = ragSettings ?? throw new ArgumentNullException(nameof(ragSettings));
        }
        public IReadOnlyList<string> ChunkText(string text, int? chunkSize = null, int? overlap = null)
        {
            int actualChunkSize = chunkSize ?? _ragSettings.ChunkSize;
            int actualOverlap = overlap ?? _ragSettings.ChunkOverlap;
            if (actualChunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (actualOverlap < 0) throw new ArgumentOutOfRangeException(nameof(overlap));
            var words = Regex.Split(text, "\\s+");
            var chunks = new List<string>();
            for (int i = 0; i < words.Length; i += actualChunkSize - actualOverlap)
            {
                int end = Math.Min(i + actualChunkSize, words.Length);
                var chunk = string.Join(" ", words, i, end - i);
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    // If chunk is too large, split recursively
                    if (end - i > actualChunkSize)
                    {
                        DocsDoc.Core.Services.LoggingService.LogInfo($"Chunk too large, splitting recursively. Size: {end - i} words");
                        var subChunks = ChunkText(chunk, actualChunkSize, 0);
                        chunks.AddRange(subChunks);
                    }
                    else
                    {
                        chunks.Add(chunk.Trim());
                    }
                }
                if (end == words.Length) break;
            }
            return chunks;
        }
        public IReadOnlyList<string> ChunkText(string text, int chunkSize, int overlap = 0)
        {
            return ChunkText(text, (int?)chunkSize, (int?)overlap);
        }
    }
} 