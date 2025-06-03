using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using DocsDoc.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocsDoc.RAG.Retrieval
{
    /// <summary>
    /// Default implementation for retrieving relevant text chunks for a query.
    /// </summary>
    public class DefaultRetrievalEngine : IRetrievalEngine
    {
        private readonly IEmbeddingProvider _embeddingProvider;
        private readonly IVectorStore _vectorStore;
        private readonly RagSettings _ragSettings;

        public DefaultRetrievalEngine(IEmbeddingProvider embeddingProvider, IVectorStore vectorStore, RagSettings ragSettings)
        {
            _embeddingProvider = embeddingProvider;
            _vectorStore = vectorStore;
            _ragSettings = ragSettings ?? throw new System.ArgumentNullException(nameof(ragSettings));
        }

        public async Task<IReadOnlyList<string>> RetrieveRelevantChunksAsync(string query, int topK, IEnumerable<string>? documentSources = null)
        {
            return await RetrieveRelevantChunksAsync(query, (int?)topK, documentSources);
        }

        public async Task<IReadOnlyList<string>> RetrieveRelevantChunksAsync(string query, int? topK = null, IEnumerable<string>? documentSources = null)
        {
            int actualTopK = topK ?? _ragSettings.RetrievalTopK;
            LoggingService.LogInfo($"Embedding query for retrieval: {query}");
            var queryEmbedding = (await _embeddingProvider.EmbedAsync(new[] { query })).FirstOrDefault();
            if (queryEmbedding == null)
                return new List<string>();
            var results = await _vectorStore.SearchAsync(queryEmbedding, actualTopK, documentSources);
            var texts = new List<string>();
            foreach (var (id, _) in results)
            {
                var text = await _vectorStore.GetTextByIdAsync(id);
                if (text != null)
                    texts.Add(text);
            }
            LoggingService.LogInfo($"Retrieved {texts.Count} relevant chunks for query{(documentSources != null ? $" from {documentSources.Count()} sources" : "")}.");
            return texts;
        }
    }
} 