using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using DocsDoc.WebScraper.Analysis;
using System.IO;
using System.Text.Json;

namespace DocsDoc.WebScraper.Analysis
{
    /// <summary>
    /// Parses XML sitemaps and robots.txt for efficient crawling. Can also build and export site graphs from cache.
    /// </summary>
    public class SiteMapParser
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly LinkDiscovery _linkDiscovery = new LinkDiscovery();

        /// <summary>
        /// Recursively gets all sitemap URLs from /sitemap.xml, sitemap indexes, and robots.txt Sitemap directives.
        /// </summary>
        public async Task<List<string>> GetAllSitemapUrlsAsync(string baseUrl)
        {
            var urls = new HashSet<string>();
            var toProcess = new Queue<string>();
            // Add robots.txt sitemaps
            var robotsSitemaps = await GetSitemapsFromRobotsTxtAsync(baseUrl);
            foreach (var s in robotsSitemaps) toProcess.Enqueue(s);
            // Add default /sitemap.xml
            toProcess.Enqueue(new Uri(new Uri(baseUrl), "/sitemap.xml").ToString());
            while (toProcess.Count > 0)
            {
                var sitemapUrl = toProcess.Dequeue();
                try
                {
                    var xml = await _httpClient.GetStringAsync(sitemapUrl);
                    var doc = XDocument.Parse(xml);
                    // If sitemapindex, enqueue all sitemaps
                    var sitemapIndexes = doc.Descendants().Where(e => e.Name.LocalName == "sitemap");
                    if (sitemapIndexes.Any())
                    {
                        foreach (var e in sitemapIndexes)
                        {
                            var loc = e.Elements().FirstOrDefault(x => x.Name.LocalName == "loc")?.Value.Trim();
                            if (!string.IsNullOrWhiteSpace(loc)) toProcess.Enqueue(loc);
                        }
                        continue;
                    }
                    // Otherwise, add all <loc> URLs
                    foreach (var e in doc.Descendants().Where(e => e.Name.LocalName == "loc"))
                    {
                        var loc = e.Value.Trim();
                        if (!string.IsNullOrWhiteSpace(loc)) urls.Add(loc);
                    }
                }
                catch (Exception) { /* Ignore errors, continue */ }
            }
            return urls.ToList();
        }

        /// <summary>
        /// Gets sitemap URLs from robots.txt Sitemap: directives.
        /// </summary>
        public async Task<List<string>> GetSitemapsFromRobotsTxtAsync(string baseUrl)
        {
            var sitemaps = new List<string>();
            try
            {
                var robotsUrl = new Uri(new Uri(baseUrl), "/robots.txt").ToString();
                var text = await _httpClient.GetStringAsync(robotsUrl);
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = trimmed.Substring(8).Trim();
                        if (!string.IsNullOrWhiteSpace(path))
                            sitemaps.Add(path);
                    }
                }
            }
            catch (Exception) { /* Ignore errors */ }
            return sitemaps;
        }

        public async Task<List<string>> GetSitemapUrlsAsync(string baseUrl)
        {
            // For backward compatibility, just call GetAllSitemapUrlsAsync
            return await GetAllSitemapUrlsAsync(baseUrl);
        }

        public async Task<RobotsTxtInfo> GetRobotsTxtAsync(string baseUrl)
        {
            var info = new RobotsTxtInfo();
            try
            {
                var robotsUrl = new Uri(new Uri(baseUrl), "/robots.txt").ToString();
                var text = await _httpClient.GetStringAsync(robotsUrl);
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = trimmed.Substring(9).Trim();
                        if (!string.IsNullOrWhiteSpace(path))
                            info.Disallow.Add(path);
                    }
                    else if (trimmed.StartsWith("Crawl-delay:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(trimmed.Substring(12).Trim(), out int delay))
                            info.CrawlDelay = delay;
                    }
                }
            }
            catch (Exception) { /* Ignore errors, return defaults */ }
            return info;
        }

        /// <summary>
        /// Builds a site graph from cached HTML files in the given directory.
        /// </summary>
        public SiteGraph BuildGraphFromCache(string cacheDir)
        {
            var graph = new SiteGraph();
            if (!Directory.Exists(cacheDir)) return graph;
            var files = Directory.GetFiles(cacheDir, "*.html");
            foreach (var file in files)
            {
                var url = TryGetUrlFromCacheFileName(file);
                if (string.IsNullOrWhiteSpace(url)) continue;
                var html = File.ReadAllText(file);
                var links = _linkDiscovery.ExtractLinks(html, url);
                graph.AddNode(url);
                foreach (var link in links)
                {
                    graph.AddEdge(url, link.Url);
                }
            }
            return graph;
        }

        /// <summary>
        /// Exports the site graph to DOT (Graphviz) format.
        /// </summary>
        public void ExportToDot(SiteGraph graph, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("digraph sitemap {");
                foreach (var node in graph.Nodes)
                {
                    writer.WriteLine($"  \"{node.Url}\";");
                }
                foreach (var edge in graph.Edges)
                {
                    writer.WriteLine($"  \"{edge.From}\" -> \"{edge.To}\";");
                }
                writer.WriteLine("}");
            }
        }

        /// <summary>
        /// Exports the site graph to JSON format (nodes and edges).
        /// </summary>
        public void ExportToJson(SiteGraph graph, string filePath)
        {
            var export = new
            {
                nodes = graph.Nodes.Select(n => n.Url).ToList(),
                edges = graph.Edges.Select(e => new { from = e.From, to = e.To }).ToList()
            };
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(export, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Attempts to recover the original URL from a cache file name (reverse of GetCacheFileName logic).
        /// </summary>
        private string TryGetUrlFromCacheFileName(string filePath)
        {
            // This is a placeholder. In production, store a mapping of hash->url in cache.
            // For now, return file name as a dummy URL.
            return Path.GetFileNameWithoutExtension(filePath);
        }
    }

    /// <summary>
    /// Represents a site graph for visualization.
    /// </summary>
    public class SiteGraph
    {
        public HashSet<SiteNode> Nodes { get; } = new HashSet<SiteNode>();
        public HashSet<SiteEdge> Edges { get; } = new HashSet<SiteEdge>();
        public void AddNode(string url)
        {
            Nodes.Add(new SiteNode(url));
        }
        public void AddEdge(string from, string to)
        {
            var fromNode = new SiteNode(from);
            var toNode = new SiteNode(to);
            Nodes.Add(fromNode);
            Nodes.Add(toNode);
            Edges.Add(new SiteEdge(from, to));
        }
    }
    public class SiteNode
    {
        public string Url { get; }
        public SiteNode(string url) { Url = url; }
        public override bool Equals(object? obj) => obj is SiteNode n && n.Url == Url;
        public override int GetHashCode() => Url.GetHashCode();
    }
    public class SiteEdge
    {
        public string From { get; }
        public string To { get; }
        public SiteEdge(string from, string to) { From = from; To = to; }
        public override bool Equals(object? obj) => obj is SiteEdge e && e.From == From && e.To == To;
        public override int GetHashCode() => (From + "->" + To).GetHashCode();
    }

    public class RobotsTxtInfo
    {
        public List<string> Disallow { get; set; } = new List<string>();
        public int? CrawlDelay { get; set; } = null;
        public bool IsPathAllowed(string path)
        {
            foreach (var dis in Disallow)
            {
                if (path.StartsWith(dis))
                    return false;
            }
            return true;
        }
    }
} 