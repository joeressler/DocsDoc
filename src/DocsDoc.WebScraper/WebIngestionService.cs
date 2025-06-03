using DocsDoc.WebScraper.Analysis;
using DocsDoc.WebScraper.Crawling;
using DocsDoc.WebScraper.Extraction;
using DocsDoc.WebScraper.Deduplication;
using DocsDoc.WebScraper.RateLimit;
using DocsDoc.WebScraper.Progress;
using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using DocsDoc.Core.Models;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
        private readonly IEmbeddingProvider _embedder;
        private readonly IVectorStore _vectorStore;
        private readonly WebScraperSettings _webScraperSettings;
        private readonly RagSettings _ragSettings;

        /// <summary>
        /// Construct with RAG pipeline dependencies.
        /// </summary>
        public WebIngestionService(
            IDocumentProcessor docProcessor, 
            ITextChunker chunker, 
            IEmbeddingProvider embedder, 
            IVectorStore vectorStore,
            WebScraperSettings webScraperSettings,
            RagSettings ragSettings)
        {
            LoggingService.LogInfo("Initializing WebIngestionService with WebScraperSettings and RagSettings");
            _docProcessor = docProcessor;
            _chunker = chunker;
            _embedder = embedder;
            _vectorStore = vectorStore;
            _webScraperSettings = webScraperSettings ?? throw new ArgumentNullException(nameof(webScraperSettings));
            _ragSettings = ragSettings ?? throw new ArgumentNullException(nameof(ragSettings));
            _analyzer = new UrlAnalyzer();
            _crawler = new WebCrawler(_webScraperSettings);
            _extractor = new ContentExtractor();
            _dedup = new ContentDeduplicator();
            _rateLimiter = new RateLimiter(_webScraperSettings);
            _progress = new ProgressTracker();
            LoggingService.LogInfo("WebIngestionService initialized successfully with WebScraperSettings and RagSettings");
        }

        /// <summary>
        /// Ingest a URL (file or docs site) and index all discovered content into the RAG pipeline.
        /// </summary>
        public async Task IngestUrlAsync(string url, string groupName = null, string baseDocsUrl = null, int? chunkSize = null, int? overlap = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var currentChunkSize = chunkSize ?? _ragSettings.ChunkSize;
            var currentOverlap = overlap ?? _ragSettings.ChunkOverlap;

            LoggingService.LogInfo($"Starting URL ingestion: {url} (chunkSize: {currentChunkSize}, overlap: {currentOverlap})");
            progress?.Report($"Starting ingestion: {url}");
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Dynamically allow the base domain of the entered URL if not already present
                var enteredDomain = new Uri(url).Host;
                if (_webScraperSettings.AllowedDomains == null)
                    _webScraperSettings.AllowedDomains = new List<string>();
                if (!_webScraperSettings.AllowedDomains.Contains(enteredDomain, StringComparer.OrdinalIgnoreCase))
                    _webScraperSettings.AllowedDomains.Add(enteredDomain);

                var type = _analyzer.Analyze(url);
                LoggingService.LogInfo($"URL analysis result: {url} -> {type}");
                progress?.Report($"Analyzed URL: {url} as {type}");
                cancellationToken.ThrowIfCancellationRequested();
                if (type == UrlType.File)
                {
                    await IngestFileUrlAsync(url, currentChunkSize, currentOverlap, progress, cancellationToken);
                    return;
                }
                if (type == UrlType.DocsSite)
                {
                    var docsBase = baseDocsUrl ?? new Uri(url).GetLeftPart(UriPartial.Authority);
                    await IngestDocsSiteAsync(url, groupName, docsBase, currentChunkSize, currentOverlap, progress, cancellationToken);
                    return;
                }
                LoggingService.LogInfo($"URL type not supported for ingestion: {type}");
                progress?.Report($"URL type not supported for ingestion: {type}");
            }
            catch (OperationCanceledException)
            {
                progress?.Report($"Ingestion cancelled: {url}");
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during URL ingestion: {url}", ex);
                progress?.Report($"Error: {ex.Message}");
                throw;
            }
        }

        private async Task IngestFileUrlAsync(string url, int chunkSize, int overlap, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            LoggingService.LogInfo($"Starting file URL ingestion: {url}");
            string tempFile = Path.GetTempFileName();
            
            try
            {
                progress?.Report("Downloading file...");
                LoggingService.LogInfo($"Downloading file from: {url}");
                
                // Use HttpRequestMessage to support cancellation
                using (var client = new System.Net.Http.HttpClient())
                using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url))
                using (var response = await client.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(tempFile, data, cancellationToken);
                    LoggingService.LogInfo($"File downloaded successfully, size: {data.Length} bytes");
                }
                
                progress?.Report("Processing file content...");
                var text = await _docProcessor.ExtractTextAsync(tempFile);
                LoggingService.LogInfo($"Text extracted from file, length: {text.Length}");
                
                var documentSource = $"{Path.GetFileName(url)}|{url}";
                await IngestTextAsync(text, documentSource, url, chunkSize, overlap, progress, cancellationToken);
                progress?.Report($"Ingested file: {url}");
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

        private async Task IngestDocsSiteAsync(string url, string groupName, string baseUrl, int chunkSize, int overlap, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            LoggingService.LogInfo($"Starting docs site ingestion: {url}");
            int count = 0;
            int duplicateCount = 0;
            
            await foreach (var (pageUrl, html) in _crawler.Crawl(url, baseUrl).WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _rateLimiter.WaitIfNeeded(new Uri(pageUrl).Host);
                    
                    var content = _extractor.Extract(html);
                    LoggingService.LogInfo($"Content extracted from: {pageUrl}, length: {content.Length}");
                    
                    if (_dedup.IsDuplicate(content))
                    {
                        duplicateCount++;
                        LoggingService.LogInfo($"Duplicate content detected and skipped: {pageUrl}");
                        progress?.Report($"Duplicate skipped: {pageUrl}");
                        continue;
                    }
                    
                    var documentSource = $"{groupName}|{baseUrl}|{pageUrl}";
                    await IngestTextAsync(content, documentSource, pageUrl, chunkSize, overlap, progress, cancellationToken);
                    progress?.Report($"Ingested: {pageUrl} (content length: {content.Length})");
                    count++;
                    LoggingService.LogInfo($"Page ingested successfully: {pageUrl}");
                }
                catch (OperationCanceledException)
                {
                    progress?.Report($"Ingestion cancelled: {pageUrl}");
                    throw;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error processing page: {pageUrl}", ex);
                    progress?.Report($"Error processing {pageUrl}: {ex.Message}");
                }
            }
            
            LoggingService.LogInfo($"Docs site crawling completed. {count} pages ingested, {duplicateCount} duplicates skipped");
            progress?.Report($"Crawling complete. {count} pages ingested.");
        }

        /// <summary>
        /// Helper to chunk, embed, and store text in the vector store.
        /// </summary>
        private async Task IngestTextAsync(string text, string documentSource, string pageUrl, int chunkSize, int overlap, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            LoggingService.LogInfo($"Starting text ingestion for source: {pageUrl}");

            cancellationToken.ThrowIfCancellationRequested();
            var chunks = _chunker.ChunkText(text, chunkSize, overlap);

            // Filter or split chunks that are too long for the model
            int maxContext = _ragSettings?.ChunkSize ?? 1024; // Or get from model settings
            var safeChunks = new List<string>();
            foreach (var chunk in chunks)
            {
                if (chunk.Length > maxContext)
                {
                    LoggingService.LogInfo($"Chunk too long for context window, splitting further. Source: {pageUrl}");
                    // Split further (e.g., by half, or by sentence)
                    var subChunks = _chunker.ChunkText(chunk, maxContext, 0);
                    safeChunks.AddRange(subChunks);
                }
                else
                {
                    safeChunks.Add(chunk);
                }
            }

            LoggingService.LogInfo($"Text chunked into {safeChunks.Count} safe chunks for source: {pageUrl}");

            var embeddings = await _embedder.EmbedAsync(safeChunks);
            LoggingService.LogInfo($"Generated {embeddings.Count} embeddings for source: {pageUrl}");

            var ids = safeChunks.Select((_, i) => $"{documentSource}_{pageUrl}_{i}");
            await _vectorStore.AddAsync(embeddings, safeChunks, ids);
            LoggingService.LogInfo($"Text ingestion completed for source: {pageUrl}");
            progress?.Report($"Text ingestion completed for source: {pageUrl}");
        }
    }
} 