using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace DocsDoc.WebScraper.Analysis
{
    /// <summary>
    /// Identifies documentation patterns and API reference structures in URLs.
    /// </summary>
    public enum LinkType { Content, TOC, Sidebar, Next, Prev, Unknown }

    public class DiscoveredLink
    {
        public string Url { get; set; } = string.Empty;
        public string AnchorText { get; set; } = string.Empty;
        public LinkType Type { get; set; } = LinkType.Unknown;
    }

    public class LinkDiscovery
    {
        /// <summary>
        /// Extracts and classifies links from HTML, identifying doc-specific patterns.
        /// </summary>
        public List<DiscoveredLink> ExtractLinks(string html, string baseUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var links = new List<DiscoveredLink>();
            var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchorNodes == null) return links;
            foreach (var node in anchorNodes)
            {
                var href = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href)) continue;
                var text = node.InnerText.Trim();
                var type = ClassifyLink(node, text, href);
                var absUrl = MakeAbsoluteUrl(baseUrl, href);
                links.Add(new DiscoveredLink { Url = absUrl, AnchorText = text, Type = type });
            }
            return links;
        }

        /// <summary>
        /// Classifies a link node based on common doc patterns.
        /// </summary>
        private LinkType ClassifyLink(HtmlNode node, string text, string href)
        {
            var lowerText = text.ToLowerInvariant();
            var lowerHref = href.ToLowerInvariant();
            var cls = node.GetAttributeValue("class", string.Empty).ToLowerInvariant();
            var id = node.GetAttributeValue("id", string.Empty).ToLowerInvariant();
            if (cls.Contains("sidebar") || id.Contains("sidebar") || lowerHref.Contains("sidebar"))
                return LinkType.Sidebar;
            if (cls.Contains("toc") || id.Contains("toc") || lowerHref.Contains("toc") || lowerText.Contains("table of contents"))
                return LinkType.TOC;
            if (cls.Contains("next") || id.Contains("next") || lowerHref.Contains("next") || lowerText == "next")
                return LinkType.Next;
            if (cls.Contains("prev") || id.Contains("prev") || lowerHref.Contains("prev") || lowerText == "previous" || lowerText == "prev")
                return LinkType.Prev;
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 2)
                return LinkType.Content;
            return LinkType.Unknown;
        }

        /// <summary>
        /// Converts a possibly relative URL to absolute, using the base URL.
        /// </summary>
        private string MakeAbsoluteUrl(string baseUrl, string href)
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
                return abs.ToString();
            if (Uri.TryCreate(new Uri(baseUrl), href, out var rel))
                return rel.ToString();
            return href;
        }
    }
} 