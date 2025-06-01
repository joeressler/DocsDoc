using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
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
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;

        public DefaultRetrievalEngine(IEmbeddingService embeddingService, IVectorStore vectorStore)
        {
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
        }

        public async Task<IReadOnlyList<string>> RetrieveRelevantChunksAsync(string query, int topK, IEnumerable<string>? documentSources = null)
        {
            LoggingService.LogInfo($"Embedding query for retrieval: {query}");
            var queryEmbedding = (await _embeddingService.EmbedAsync(new[] { query })).FirstOrDefault();
            if (queryEmbedding == null)
                return new List<string>();
            var results = await _vectorStore.SearchAsync(queryEmbedding, topK, documentSources);
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