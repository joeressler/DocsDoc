using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocsDoc.Core.Services;
using System.Linq;

namespace DocsDoc.WebScraper.Crawling
{
    /// <summary>
    /// Crawls documentation sites, respecting domain and depth limits.
    /// </summary>
    public class WebCrawler
    {
        private readonly HttpClient _httpClient;
        private readonly int _maxDepth;
        private readonly List<string>? _allowedDomains;
        private readonly string? _cachePath;
        private readonly int _maxConcurrentRequests;

        public WebCrawler(
            string? userAgent = null, 
            int maxDepth = 2, 
            List<string>? allowedDomains = null, 
            string? cachePath = null,
            int maxConcurrentRequests = 2)
        {
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(userAgent))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            }
            _maxDepth = maxDepth;
            _allowedDomains = allowedDomains;
            _cachePath = cachePath;
            _maxConcurrentRequests = maxConcurrentRequests;
        }

        /// <summary>
        /// Crawl a site starting from startUrl, up to maxDepth and domain limit.
        /// </summary>
        public async IAsyncEnumerable<(string url, string html)> Crawl(string startUrl)
        {
            LoggingService.LogInfo($"Starting web crawl from: {startUrl}, configured maxDepth: {_maxDepth}");
            
            var queue = new Queue<(string url, int depth)>();
            var visited = new HashSet<string>();
            queue.Enqueue((startUrl, 0));
            
            string primaryDomain = new Uri(startUrl).Host;
            LoggingService.LogInfo($"Crawling primary domain: {primaryDomain}");

            if (_allowedDomains != null && _allowedDomains.Any() && !_allowedDomains.Contains(primaryDomain, StringComparer.OrdinalIgnoreCase))
            {
                LoggingService.LogError($"The start URL's domain '{primaryDomain}' is not in the allowed domains list. Crawling will be restricted or might not proceed as expected.", null);
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
                
                if (visited.Contains(url) || depth > _maxDepth || !isAllowedDomain)
                {
                    LoggingService.LogInfo($"Skipping URL: {url} (visited: {visited.Contains(url)}, depth: {depth}/{_maxDepth}, domain allowed: {isAllowedDomain})");
                    continue;
                }
                
                visited.Add(url);
                LoggingService.LogInfo($"Processing URL: {url} (depth: {depth})");
                
                string html = string.Empty;
                try 
                { 
                    html = await _httpClient.GetStringAsync(url);
                    processedCount++;
                    LoggingService.LogInfo($"Successfully fetched content from: {url} (length: {html.Length})");
                } 
                catch (Exception ex)
                {
                    LoggingService.LogError($"Failed to fetch content from: {url}", ex);
                    continue;
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
    }
} 