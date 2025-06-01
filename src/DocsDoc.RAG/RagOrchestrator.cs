using DocsDoc.Core.Interfaces;
using DocsDoc.RAG.Processing;
using DocsDoc.RAG.Embedding;
using DocsDoc.RAG.Storage;
using DocsDoc.RAG.Retrieval;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using DocsDoc.WebScraper;

namespace DocsDoc.RAG
{
    /// <summary>
    /// Orchestrates the full RAG pipeline: ingestion and query.
    /// </summary>
    public class RagOrchestrator : IDisposable
    {
        private readonly IDocumentProcessor _docProcessor;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embedder;
        private readonly IVectorStore _vectorStore;
        private readonly IRetrievalEngine _retriever;
        private readonly IContextAugmenter _contextAugmenter;
        private readonly ILlamaSharpInferenceService _llm;
        private bool _disposed = false;

        /// <summary>
        /// Exposes the LlamaSharp inference service for direct access to chat and generation APIs.
        /// </summary>
        public ILlamaSharpInferenceService LlmService => _llm;

        /// <summary>
        /// Exposes the vector store for document management operations.
        /// </summary>
        public IVectorStore VectorStore => _vectorStore;

        public RagOrchestrator(string modelPath, string dbPath)
        {
            _docProcessor = new DefaultDocumentProcessor();
            _chunker = new DefaultTextChunker();
            _llm = new LlamaSharpInferenceService(modelPath);
            _embedder = new LlamaSharpEmbeddingService(_llm);
            _vectorStore = new SqliteVectorStore(dbPath);
            _retriever = new DefaultRetrievalEngine(_embedder, _vectorStore);
            _contextAugmenter = new DefaultContextAugmenter();
        }

        /// <summary>
        /// Ingest a document: extract, chunk, embed, and store.
        /// </summary>
        public async Task IngestDocumentAsync(string filePath, int chunkSize = 200, int overlap = 50)
        {
            var text = await _docProcessor.ExtractTextAsync(filePath);
            var chunks = _chunker.ChunkText(text, chunkSize, overlap);
            var embeddings = await _embedder.EmbedAsync(chunks);
            var ids = chunks.Select((_, i) => $"{Path.GetFileName(filePath)}_{i}");
            await _vectorStore.AddAsync(embeddings, chunks, ids);
        }

        /// <summary>
        /// Query with RAG: retrieve, augment, and generate.
        /// </summary>
        /// <param name="userQuery">User's query</param>
        /// <param name="topK">Number of top chunks to retrieve</param>
        /// <param name="inferenceParams">LLM inference parameters</param>
        /// <param name="documentSources">Optional filter to limit retrieval to specific document sources</param>
        public async Task<string> QueryAsync(string userQuery, int topK = 5, object? inferenceParams = null, IEnumerable<string>? documentSources = null)
        {
            var contextChunks = await _retriever.RetrieveRelevantChunksAsync(userQuery, topK, documentSources);
            var prompt = _contextAugmenter.BuildPrompt(userQuery, contextChunks);
            return await _llm.GenerateAsync(prompt, inferenceParams);
        }

        /// <summary>
        /// Get a WebIngestionService wired to this orchestrator's RAG pipeline.
        /// </summary>
        public WebIngestionService GetWebIngestionService()
        {
            return new WebIngestionService(_docProcessor, _chunker, _embedder, _vectorStore);
        }

        /// <summary>
        /// Dispose orchestrator and all resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            (_llm as IDisposable)?.Dispose();
            (_vectorStore as IDisposable)?.Dispose();
            _disposed = true;
        }
    }
} 