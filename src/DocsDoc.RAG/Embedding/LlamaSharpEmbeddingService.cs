using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocsDoc.RAG.Embedding
{
    /// <summary>
    /// Embedding service using LlamaSharpInferenceService.
    /// </summary>
    public class LlamaSharpEmbeddingService : IEmbeddingService
    {
        private readonly ILlamaSharpInferenceService _llamaService;

        public LlamaSharpEmbeddingService(ILlamaSharpInferenceService llamaService)
        {
            _llamaService = llamaService;
        }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> texts)
        {
            var results = new List<float[]>();
            int i = 0;
            foreach (var text in texts)
            {
                try
                {
                    LoggingService.LogInfo($"Embedding chunk {i + 1}");
                    var embedding = await _llamaService.GetEmbeddingAsync(text);
                    if (embedding.Count > 0)
                        results.Add(embedding[0]);
                    else
                        LoggingService.LogInfo($"No embedding returned for chunk {i + 1}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error embedding chunk {i + 1}", ex);
                    throw;
                }
                i++;
            }
            return results;
        }
    }
} 