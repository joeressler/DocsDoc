using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocsDoc.Core.Services;
using System.Linq;
using DocsDoc.Core.Models;
using DocsDoc.WebScraper.Analysis;
using System.Threading;
using DocsDoc.WebScraper.Progress;
using DocsDoc.WebScraper.RateLimit;
using Polly;

namespace DocsDoc.WebScraper.Crawling
{
    /// <summary>
    /// Crawls documentation sites, respecting domain and depth limits.
    /// </summary>
    public class WebCrawler : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly int _maxDepth;
        private readonly List<string>? _allowedDomains;
        private readonly string? _cachePath;
        private readonly int _maxConcurrentRequests;
        private readonly WebScraperSettings _settings;
        private readonly SiteMapParser _siteMapParser = new SiteMapParser();
        private RobotsTxtInfo? _robots;
        private SemaphoreSlim? _semaphore;
        private bool _disposed = false;
        private readonly ProgressTracker? _progressTracker;
        private readonly RateLimiter? _rateLimiter;
        private readonly LinkDiscovery _linkDiscovery = new LinkDiscovery();
        private readonly HashSet<string>? _whitelist;
        private readonly HashSet<string>? _blacklist;
        private readonly string? _checkpointPath;

        // Event hooks
        public Action<string>? OnProgress;
        public Action<string, Exception>? OnError;
        public Action<string, List<DiscoveredLink>>? OnLinkDiscovered;

        public WebCrawler(WebScraperSettings settings, ProgressTracker? progressTracker = null, RateLimiter? rateLimiter = null, IEnumerable<string>? whitelist = null, IEnumerable<string>? blacklist = null, string? checkpointPath = null)
            : this(settings)
        {
            _progressTracker = progressTracker;
            _rateLimiter = rateLimiter;
            _whitelist = whitelist != null ? new HashSet<string>(whitelist, StringComparer.OrdinalIgnoreCase) : null;
            _blacklist = blacklist != null ? new HashSet<string>(blacklist, StringComparer.OrdinalIgnoreCase) : null;
            _checkpointPath = checkpointPath;
        }

        public WebCrawler(WebScraperSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_settings.UserAgent))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
            }
            _maxDepth = _settings.MaxCrawlDepth;
            _allowedDomains = _settings.AllowedDomains;
            _cachePath = _settings.CachePath;
            _maxConcurrentRequests = _settings.MaxConcurrentRequests;
            _semaphore = new SemaphoreSlim(_maxConcurrentRequests);
        }

        /// <summary>
        /// Crawl a site starting from startUrl, up to maxDepth and domain limit. Supports politeness, progress, and checkpointing.
        /// </summary>
        public async IAsyncEnumerable<(string url, string html)> Crawl(string startUrl, string? baseDocsUrl = null, CancellationToken cancellationToken = default)
        {
            LoggingService.LogInfo($"Starting web crawl from: {startUrl}, configured maxDepth: {_maxDepth}, baseDocsUrl restriction: {baseDocsUrl}");
            _progressTracker?.Update($"Crawl started: {startUrl}");
            OnProgress?.Invoke($"Crawl started: {startUrl}");

            var queue = new Queue<(string url, int depth)>();
            var visited = new HashSet<string>();
            // Checkpoint resume
            if (!string.IsNullOrWhiteSpace(_checkpointPath) && System.IO.File.Exists(_checkpointPath))
            {
                try
                {
                    var lines = await System.IO.File.ReadAllLinesAsync(_checkpointPath, cancellationToken);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Q:")) queue.Enqueue((line.Substring(2), 0));
                        else if (line.StartsWith("V:")) visited.Add(line.Substring(2));
                    }
                    _progressTracker?.Update($"Resumed from checkpoint: {queue.Count} queued, {visited.Count} visited");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to load checkpoint", ex);
                    OnError?.Invoke("Failed to load checkpoint", ex);
                }
            }
            if (queue.Count == 0) queue.Enqueue((startUrl, 0));

            string primaryDomain = new Uri(startUrl).Host;
            LoggingService.LogInfo($"Crawling primary domain: {primaryDomain}");

            // Fetch robots.txt
            _robots = await _siteMapParser.GetRobotsTxtAsync(startUrl);
            if (_robots.CrawlDelay.HasValue)
                LoggingService.LogInfo($"robots.txt crawl-delay: {_robots.CrawlDelay.Value}");

            // Fetch and enqueue sitemap URLs
            var sitemapUrls = await _siteMapParser.GetSitemapUrlsAsync(startUrl);
            foreach (var url in sitemapUrls)
            {
                queue.Enqueue((url, 1));
            }

            int processedCount = 0;

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (url, depth) = queue.Dequeue();
                var currentUri = new Uri(url);

                // Whitelist/blacklist check
                if ((_whitelist != null && !_whitelist.Contains(currentUri.Host)) || (_blacklist != null && _blacklist.Contains(currentUri.Host)))
                {
                    LoggingService.LogInfo($"Skipping URL (whitelist/blacklist): {url}");
                    continue;
                }

                bool isAllowedDomain = true;
                if (_allowedDomains != null && _allowedDomains.Any())
                {
                    isAllowedDomain = _allowedDomains.Contains(currentUri.Host, StringComparer.OrdinalIgnoreCase);
                }

                bool isAllowedPrefix = string.IsNullOrEmpty(baseDocsUrl) || url.StartsWith(baseDocsUrl, StringComparison.OrdinalIgnoreCase);
                if (visited.Contains(url) || depth > _maxDepth || !isAllowedDomain || !isAllowedPrefix)
                {
                    LoggingService.LogInfo($"Skipping URL: {url} (visited: {visited.Contains(url)}, depth: {depth}/{_maxDepth}, domain allowed: {isAllowedDomain}, prefix allowed: {isAllowedPrefix})");
                    continue;
                }

                if (_robots != null && !_robots.IsPathAllowed(currentUri.AbsolutePath))
                {
                    LoggingService.LogInfo($"robots.txt disallows: {url}");
                    continue;
                }

                visited.Add(url);
                _progressTracker?.Update($"Processing: {url}");
                OnProgress?.Invoke($"Processing: {url}");
                LoggingService.LogInfo($"Processing URL: {url} (depth: {depth})");

                await (_semaphore?.WaitAsync() ?? Task.CompletedTask);
                string html = string.Empty;
                try
                {
                    // robots.txt crawl-delay
                    if (_robots?.CrawlDelay is int delay && delay > 0)
                        await Task.Delay(delay * 1000, cancellationToken);
                    // RateLimiter politeness
                    if (_rateLimiter != null)
                        await _rateLimiter.WaitIfNeeded(currentUri.Host);
                    // Polly retry policy for HTTP fetch
                    var policy = Policy
                        .Handle<HttpRequestException>()
                        .Or<TaskCanceledException>()
                        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                            (ex, ts, retry, ctx) =>
                            {
                                LoggingService.LogError($"Retry {retry} for {url} after {ts.TotalSeconds}s due to: {ex.Message}", ex);
                                _progressTracker?.OnError(url, ex);
                                OnError?.Invoke(url, ex);
                            });
                    await policy.ExecuteAsync(async (ctx, ct) =>
                    {
                        // Check cache
                        if (!string.IsNullOrWhiteSpace(_cachePath))
                        {
                            var cacheFile = System.IO.Path.Combine(_cachePath, GetCacheFileName(url));
                            if (System.IO.File.Exists(cacheFile))
                            {
                                html = await System.IO.File.ReadAllTextAsync(cacheFile, ct);
                                LoggingService.LogInfo($"Loaded from cache: {url}");
                            }
                            else
                            {
                                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
                                {
                                    response.EnsureSuccessStatusCode();
                                    html = await response.Content.ReadAsStringAsync();
                                    System.IO.Directory.CreateDirectory(_cachePath);
                                    await System.IO.File.WriteAllTextAsync(cacheFile, html, ct);
                                    LoggingService.LogInfo($"Fetched and cached: {url}");
                                }
                            }
                        }
                        else
                        {
                            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
                            {
                                response.EnsureSuccessStatusCode();
                                html = await response.Content.ReadAsStringAsync();
                                LoggingService.LogInfo($"Fetched (no cache): {url}");
                            }
                        }
                    }, new Polly.Context(), cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Failed to fetch content from: {url}", ex);
                    _progressTracker?.OnError(url, ex);
                    OnError?.Invoke(url, ex);
                    continue;
                }
                finally
                {
                    _semaphore?.Release();
                }

                yield return (url, html);

                // Link discovery
                var discoveredLinks = _linkDiscovery.ExtractLinks(html, currentUri.GetLeftPart(UriPartial.Authority));
                OnLinkDiscovered?.Invoke(url, discoveredLinks);
                int linkCount = 0;
                foreach (var link in discoveredLinks.Select(l => l.Url))
                {
                    if (!visited.Contains(link))
                    {
                        queue.Enqueue((link, depth + 1));
                        linkCount++;
                    }
                }
                LoggingService.LogInfo($"Discovered {linkCount} new links from: {url}");
                _progressTracker?.Update($"Discovered {linkCount} links from: {url}");
                OnProgress?.Invoke($"Discovered {linkCount} links from: {url}");

                // Save checkpoint
                if (!string.IsNullOrWhiteSpace(_checkpointPath))
                {
                    try
                    {
                        var lines = queue.Select(q => $"Q:{q.url}").Concat(visited.Select(v => $"V:{v}"));
                        System.IO.File.WriteAllLines(_checkpointPath, lines);
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError("Failed to save checkpoint", ex);
                        OnError?.Invoke("Failed to save checkpoint", ex);
                    }
                }
            }

            LoggingService.LogInfo($"Web crawl completed. Processed {processedCount} pages, visited {visited.Count} URLs total");
            _progressTracker?.Update($"Crawl completed. {processedCount} pages processed.");
            OnProgress?.Invoke($"Crawl completed. {processedCount} pages processed.");
        }

        private string GetCacheFileName(string url)
        {
            var hash = url.GetHashCode().ToString("X");
            return hash + ".html";
        }

        private IEnumerable<string> ExtractLinks(string html, string baseScheme, string baseDomain)
        {
            var doubleQuoteMatches = Regex.Matches(html, "href=\"([^\"]+)\"");
            var singleQuoteMatches = Regex.Matches(html, "href=\'([^\']+)\'");
            int extractedCount = 0;
            
            foreach (Match m in doubleQuoteMatches)
            {
                var link = m.Groups[1].Value;
                if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
                {
                    if (_allowedDomains == null || !_allowedDomains.Any() || _allowedDomains.Contains(new Uri(link).Host, StringComparer.OrdinalIgnoreCase))
                    {
                        extractedCount++;
                        yield return link;
                    }
                }
                else if (Uri.IsWellFormedUriString(link, UriKind.Relative))
                {
                    var absoluteLink = new Uri(new Uri($"{baseScheme}://{baseDomain}/"), link).ToString();
                    if (_allowedDomains == null || !_allowedDomains.Any() || _allowedDomains.Contains(new Uri(absoluteLink).Host, StringComparer.OrdinalIgnoreCase))
                    {
                        extractedCount++;
                        yield return absoluteLink;
                    }
                }
            }
            
            foreach (Match m in singleQuoteMatches)
            {
                var link = m.Groups[1].Value;
                if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
                {
                    if (_allowedDomains == null || !_allowedDomains.Any() || _allowedDomains.Contains(new Uri(link).Host, StringComparer.OrdinalIgnoreCase))
                    {
                        extractedCount++;
                        yield return link;
                    }
                }
                else if (Uri.IsWellFormedUriString(link, UriKind.Relative))
                {
                    var absoluteLink = new Uri(new Uri($"{baseScheme}://{baseDomain}/"), link).ToString();
                    if (_allowedDomains == null || !_allowedDomains.Any() || _allowedDomains.Contains(new Uri(absoluteLink).Host, StringComparer.OrdinalIgnoreCase))
                    {
                        extractedCount++;
                        yield return absoluteLink;
                    }
                }
            }
            
            LoggingService.LogInfo($"Link extraction completed, found {extractedCount} valid links matching allowed domains from {baseScheme}://{baseDomain}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _semaphore?.Dispose();
            _disposed = true;
        }
    }
} 