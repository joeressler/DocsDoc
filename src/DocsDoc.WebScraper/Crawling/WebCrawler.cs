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
        /// Crawl a site starting from startUrl, up to maxDepth and domain limit.
        /// </summary>
        public async IAsyncEnumerable<(string url, string html)> Crawl(string startUrl, string? baseDocsUrl = null)
        {
            LoggingService.LogInfo($"Starting web crawl from: {startUrl}, configured maxDepth: {_maxDepth}, baseDocsUrl restriction: {baseDocsUrl}");
            
            var queue = new Queue<(string url, int depth)>();
            var visited = new HashSet<string>();
            queue.Enqueue((startUrl, 0));
            
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
                var (url, depth) = queue.Dequeue();
                var currentUri = new Uri(url);

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
                LoggingService.LogInfo($"Processing URL: {url} (depth: {depth})");
                
                await (_semaphore?.WaitAsync() ?? Task.CompletedTask);
                string html = string.Empty;
                try 
                { 
                    // robots.txt crawl-delay
                    if (_robots?.CrawlDelay is int delay && delay > 0)
                        await Task.Delay(delay * 1000);
                    // Check cache
                    if (!string.IsNullOrWhiteSpace(_cachePath))
                    {
                        var cacheFile = System.IO.Path.Combine(_cachePath, GetCacheFileName(url));
                        if (System.IO.File.Exists(cacheFile))
                        {
                            html = await System.IO.File.ReadAllTextAsync(cacheFile);
                            LoggingService.LogInfo($"Loaded from cache: {url}");
                        }
                        else
                        {
                            html = await _httpClient.GetStringAsync(url);
                            System.IO.Directory.CreateDirectory(_cachePath);
                            await System.IO.File.WriteAllTextAsync(cacheFile, html);
                            LoggingService.LogInfo($"Fetched and cached: {url}");
                        }
                    }
                    else
                    {
                        html = await _httpClient.GetStringAsync(url);
                        LoggingService.LogInfo($"Fetched (no cache): {url}");
                    }
                    processedCount++;
                } 
                catch (Exception ex)
                {
                    LoggingService.LogError($"Failed to fetch content from: {url}", ex);
                    continue;
                }
                finally
                {
                    _semaphore?.Release();
                }
                
                yield return (url, html);
                
                var discoveredLinks = ExtractLinks(html, currentUri.Scheme, currentUri.Host);
                int linkCount = 0;
                foreach (var link in discoveredLinks)
                {
                    if (!visited.Contains(link))
                    {
                        queue.Enqueue((link, depth + 1));
                        linkCount++;
                    }
                }
                LoggingService.LogInfo($"Discovered {linkCount} new links from: {url}");
            }
            
            LoggingService.LogInfo($"Web crawl completed. Processed {processedCount} pages, visited {visited.Count} URLs total");
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