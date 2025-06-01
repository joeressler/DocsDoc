using DocsDoc.WebScraper.Analysis;
using DocsDoc.WebScraper.Crawling;
using DocsDoc.WebScraper.Extraction;
using DocsDoc.WebScraper.Deduplication;
using DocsDoc.WebScraper.RateLimit;
using DocsDoc.WebScraper.Progress;
using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DocsDoc.WebScraper
{
    /// <summary>
    /// Orchestrates web ingestion pipeline: analyze, crawl, extract, deduplicate, chunk, embed, and store.
    /// </summary>
    public class WebIngestionService
    {
        private readonly UrlAnalyzer _analyzer;
        private readonly WebCrawler _crawler;
        private readonly ContentExtractor _extractor;
        private readonly ContentDeduplicator _dedup;
        private readonly RateLimiter _rateLimiter;
        private readonly ProgressTracker _progress;
        private readonly IDocumentProcessor _docProcessor;
        private readonly ITextChunker _chunker;
        private readonly IEmbeddingService _embedder;
        private readonly IVectorStore _vectorStore;

        private readonly int _defaultChunkSize;
        private readonly int _defaultChunkOverlap;

        /// <summary>
        /// Construct with RAG pipeline dependencies.
        /// </summary>
        public WebIngestionService(
            IDocumentProcessor docProcessor, 
            ITextChunker chunker, 
            IEmbeddingService embedder, 
            IVectorStore vectorStore,
            string? userAgent = null,
            int rateLimitSeconds = 2,
            int maxConcurrentRequests = 2,
            int maxCrawlDepth = 3,
            List<string>? allowedDomains = null,
            string? cachePath = null,
            int defaultChunkSize = 200,
            int defaultChunkOverlap = 50
            )
        {
            LoggingService.LogInfo("Initializing WebIngestionService with extended configuration");
            _docProcessor = docProcessor;
            _chunker = chunker;
            _embedder = embedder;
            _vectorStore = vectorStore;

            _analyzer = new UrlAnalyzer();
            _crawler = new WebCrawler(userAgent, maxCrawlDepth, allowedDomains, cachePath, maxConcurrentRequests);
            _extractor = new ContentExtractor();
            _dedup = new ContentDeduplicator();
            _rateLimiter = new RateLimiter(TimeSpan.FromSeconds(rateLimitSeconds));
            _progress = new ProgressTracker();

            _defaultChunkSize = defaultChunkSize;
            _defaultChunkOverlap = defaultChunkOverlap;

            LoggingService.LogInfo("WebIngestionService initialized successfully with extended configuration");
        }

        /// <summary>
        /// Ingest a URL (file or docs site) and index all discovered content into the RAG pipeline.
        /// </summary>
        public async Task IngestUrlAsync(string url, int? chunkSize = null, int? overlap = null, Action<string>? progress = null)
        {
            var currentChunkSize = chunkSize ?? _defaultChunkSize;
            var currentOverlap = overlap ?? _defaultChunkOverlap;

            LoggingService.LogInfo($"Starting URL ingestion: {url} (chunkSize: {currentChunkSize}, overlap: {currentOverlap})");
            
            try
            {
                var type = _analyzer.Analyze(url);
                LoggingService.LogInfo($"URL analysis result: {url} -> {type}");
                progress?.Invoke($"Analyzed URL: {url} as {type}");
                
                if (type == UrlType.File)
                {
                    await IngestFileUrlAsync(url, currentChunkSize, currentOverlap, progress);
                    return;
                }
                
                if (type == UrlType.DocsSite)
                {
                    await IngestDocsSiteAsync(url, currentChunkSize, currentOverlap, progress);
                    return;
                }
                
                LoggingService.LogInfo($"URL type not supported for ingestion: {type}");
                progress?.Invoke($"URL type not supported for ingestion: {type}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during URL ingestion: {url}", ex);
                progress?.Invoke($"Error: {ex.Message}");
                throw;
            }
        }

        private async Task IngestFileUrlAsync(string url, int chunkSize, int overlap, Action<string>? progress)
        {
            LoggingService.LogInfo($"Starting file URL ingestion: {url}");
            string tempFile = Path.GetTempFileName();
            
            try
            {
                progress?.Invoke("Downloading file...");
                LoggingService.LogInfo($"Downloading file from: {url}");
                
                using (var client = new System.Net.Http.HttpClient())
                {
                    var data = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(tempFile, data);
                    LoggingService.LogInfo($"File downloaded successfully, size: {data.Length} bytes");
                }
                
                progress?.Invoke("Processing file content...");
                var text = await _docProcessor.ExtractTextAsync(tempFile);
                LoggingService.LogInfo($"Text extracted from file, length: {text.Length}");
                
                await IngestTextAsync(text, url, chunkSize, overlap);
                progress?.Invoke($"Ingested file: {url}");
                LoggingService.LogInfo($"File URL ingestion completed successfully: {url}");
            }
            finally 
            { 
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                    LoggingService.LogInfo("Temporary file cleaned up");
                }
            }
        }

        private async Task IngestDocsSiteAsync(string url, int chunkSize, int overlap, Action<string>? progress)
        {
            LoggingService.LogInfo($"Starting docs site ingestion: {url}");
            int count = 0;
            int duplicateCount = 0;
            
            await foreach (var (pageUrl, html) in _crawler.Crawl(url))
            {
                try
                {
                    await _rateLimiter.WaitIfNeeded(new Uri(pageUrl).Host);
                    
                    var content = _extractor.Extract(html);
                    LoggingService.LogInfo($"Content extracted from: {pageUrl}, length: {content.Length}");
                    
                    if (_dedup.IsDuplicate(content))
                    {
                        duplicateCount++;
                        LoggingService.LogInfo($"Duplicate content detected and skipped: {pageUrl}");
                        progress?.Invoke($"Duplicate skipped: {pageUrl}");
                        continue;
                    }
                    
                    await IngestTextAsync(content, pageUrl, chunkSize, overlap);
                    progress?.Invoke($"Ingested: {pageUrl} (content length: {content.Length})");
                    count++;
                    LoggingService.LogInfo($"Page ingested successfully: {pageUrl}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error processing page: {pageUrl}", ex);
                    progress?.Invoke($"Error processing {pageUrl}: {ex.Message}");
                }
            }
            
            LoggingService.LogInfo($"Docs site crawling completed. {count} pages ingested, {duplicateCount} duplicates skipped");
            progress?.Invoke($"Crawling complete. {count} pages ingested.");
        }

        /// <summary>
        /// Helper to chunk, embed, and store text in the vector store.
        /// </summary>
        private async Task IngestTextAsync(string text, string sourceId, int chunkSize, int overlap)
        {
            LoggingService.LogInfo($"Starting text ingestion for source: {sourceId}");
            
            var chunks = _chunker.ChunkText(text, chunkSize, overlap);
            LoggingService.LogInfo($"Text chunked into {chunks.Count} chunks for source: {sourceId}");
            
            var embeddings = await _embedder.EmbedAsync(chunks);
            LoggingService.LogInfo($"Generated {embeddings.Count} embeddings for source: {sourceId}");
            
            var ids = chunks.Select((_, i) => $"{sourceId}_{i}");
            await _vectorStore.AddAsync(embeddings, chunks, ids);
            LoggingService.LogInfo($"Text ingestion completed for source: {sourceId}");
        }
    }
} 