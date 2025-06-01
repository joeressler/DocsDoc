using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocsDoc.Core.Services;

namespace DocsDoc.WebScraper.Crawling
{
    /// <summary>
    /// Crawls documentation sites, respecting domain and depth limits.
    /// </summary>
    public class WebCrawler
    {
        private readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Crawl a site starting from startUrl, up to maxDepth and domain limit.
        /// </summary>
        public async IAsyncEnumerable<(string url, string html)> Crawl(string startUrl, int maxDepth = 2, string? domain = null)
        {
            LoggingService.LogInfo($"Starting web crawl from: {startUrl}, maxDepth: {maxDepth}");
            
            var queue = new Queue<(string url, int depth)>();
            var visited = new HashSet<string>();
            queue.Enqueue((startUrl, 0));
            domain ??= new Uri(startUrl).Host;
            
            LoggingService.LogInfo($"Crawling domain: {domain}");
            int processedCount = 0;
            
            while (queue.Count > 0)
            {
                var (url, depth) = queue.Dequeue();
                
                if (visited.Contains(url) || depth > maxDepth || !url.Contains(domain))
                {
                    LoggingService.LogInfo($"Skipping URL: {url} (visited: {visited.Contains(url)}, depth: {depth}/{maxDepth}, domain match: {url.Contains(domain)})");
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
                
                var discoveredLinks = ExtractLinks(html, domain);
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

        // Extract links from HTML (handles both single and double quotes, C# 8.0 compatible)
        private IEnumerable<string> ExtractLinks(string html, string domain)
        {
            var doubleQuoteMatches = Regex.Matches(html, "href=\"([^\"]+)\"");
            var singleQuoteMatches = Regex.Matches(html, "href='([^']+)'");
            int extractedCount = 0;
            
            foreach (Match m in doubleQuoteMatches)
            {
                var link = m.Groups[1].Value;
                if (Uri.IsWellFormedUriString(link, UriKind.Absolute) && link.Contains(domain))
                {
                    extractedCount++;
                    yield return link;
                }
                else if (Uri.IsWellFormedUriString(link, UriKind.Relative))
                {
                    var absoluteLink = "https://" + domain.TrimEnd('/') + "/" + link.TrimStart('/');
                    extractedCount++;
                    yield return absoluteLink;
                }
            }
            
            foreach (Match m in singleQuoteMatches)
            {
                var link = m.Groups[1].Value;
                if (Uri.IsWellFormedUriString(link, UriKind.Absolute) && link.Contains(domain))
                {
                    extractedCount++;
                    yield return link;
                }
                else if (Uri.IsWellFormedUriString(link, UriKind.Relative))
                {
                    var absoluteLink = "https://" + domain.TrimEnd('/') + "/" + link.TrimStart('/');
                    extractedCount++;
                    yield return absoluteLink;
                }
            }
            
            LoggingService.LogInfo($"Link extraction completed, found {extractedCount} valid links");
        }
    }
} 