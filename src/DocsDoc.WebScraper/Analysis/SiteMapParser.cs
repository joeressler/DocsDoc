using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocsDoc.WebScraper.Analysis
{
    /// <summary>
    /// Parses XML sitemaps and robots.txt for efficient crawling.
    /// </summary>
    public class SiteMapParser
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<List<string>> GetSitemapUrlsAsync(string baseUrl)
        {
            var urls = new List<string>();
            try
            {
                var sitemapUrl = new Uri(new Uri(baseUrl), "/sitemap.xml").ToString();
                var xml = await _httpClient.GetStringAsync(sitemapUrl);
                var doc = XDocument.Parse(xml);
                urls = doc.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value.Trim()).ToList();
            }
            catch (Exception) { /* Ignore errors, return empty list */ }
            return urls;
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