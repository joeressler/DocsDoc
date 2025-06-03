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
using DocsDoc.Core.Models;

namespace DocsDoc.RAG
{
    /// <summary>
    /// Orchestrates the full RAG pipeline: ingestion and query.
    /// </summary>
    public class RagOrchestrator : IDisposable
    {
        private readonly IDocumentProcessor _docProcessor;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingProvider _embedder;
        private readonly IVectorStore _vectorStore;
        private readonly IRetrievalEngine _retriever;
        private readonly IContextAugmenter _contextAugmenter;
        private readonly ILlamaSharpInferenceService _llm;
        private readonly ModelSettings _modelSettings;
        private readonly DatabaseSettings _databaseSettings;
        private readonly RagSettings _ragSettings;
        private bool _disposed = false;

        private readonly int _ragChunkSize;
        private readonly int _ragChunkOverlap;
        private readonly int _ragRetrievalTopK;

        /// <summary>
        /// Exposes the LlamaSharp inference service for direct access to chat and generation APIs.
        /// </summary>
        public ILlamaSharpInferenceService LlmService => _llm;

        /// <summary>
        /// Exposes the vector store for document management operations.
        /// </summary>
        public IVectorStore VectorStore => _vectorStore;

        public IRetrievalEngine Retriever => _retriever;

        public RagOrchestrator(ModelSettings modelSettings, DatabaseSettings databaseSettings, RagSettings ragSettings,
                                int ragChunkSize = 512, int ragChunkOverlap = 64, int ragRetrievalTopK = 5)
        {
            _modelSettings = modelSettings;
            _databaseSettings = databaseSettings;
            _ragSettings = ragSettings;
            _docProcessor = new DefaultDocumentProcessor();
            _chunker = new DefaultTextChunker(_ragSettings);
            _llm = new LlamaSharpInferenceService(_modelSettings);
            _embedder = new LlamaSharpEmbeddingService(_modelSettings);
            _vectorStore = new SqliteVectorStore(_databaseSettings);
            _retriever = new DefaultRetrievalEngine(_embedder, _vectorStore, _ragSettings);
            _contextAugmenter = new DefaultContextAugmenter();

            _ragChunkSize = ragChunkSize;
            _ragChunkOverlap = ragChunkOverlap;
            _ragRetrievalTopK = ragRetrievalTopK;
        }

        /// <summary>
        /// Ingest a document: extract, chunk, embed, and store.
        /// </summary>
        public async Task IngestDocumentAsync(string filePath, int? chunkSize = null, int? overlap = null)
        {
            var currentChunkSize = chunkSize ?? _ragChunkSize;
            var currentOverlap = overlap ?? _ragChunkOverlap;
            var text = await _docProcessor.ExtractTextAsync(filePath);
            var chunks = _chunker.ChunkText(text, currentChunkSize, currentOverlap);
            var embeddings = await _embedder.EmbedAsync(chunks);
            var documentSourceIds = Enumerable.Repeat(filePath, chunks.Count).ToList();
            await _vectorStore.AddAsync(embeddings, chunks, documentSourceIds);
        }

        /// <summary>
        /// Query with RAG: retrieve, augment, and generate.
        /// </summary>
        /// <param name="userQuery">User's query</param>
        /// <param name="topK">Number of top chunks to retrieve</param>
        /// <param name="inferenceParams">LLM inference parameters</param>
        /// <param name="documentSources">Optional filter to limit retrieval to specific document sources</param>
        public async Task<string> QueryAsync(string userQuery, int? topK = null, object? inferenceParams = null, IEnumerable<string>? documentSources = null)
        {
            var currentTopK = topK ?? _ragRetrievalTopK;
            var contextChunks = await _retriever.RetrieveRelevantChunksAsync(userQuery, currentTopK, documentSources);
            var prompt = _contextAugmenter.BuildPrompt(userQuery, contextChunks);
            return await _llm.GenerateAsync(prompt, inferenceParams);
        }

        /// <summary>
        /// Get a WebIngestionService wired to this orchestrator's RAG pipeline.
        /// </summary>
        public WebIngestionService GetWebIngestionService(WebScraperSettings webScraperSettings, RagSettings ragSettings)
        {
            return new WebIngestionService(_docProcessor, _chunker, _embedder, _vectorStore, webScraperSettings, ragSettings, _embedder as ITokenCountingProvider);
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