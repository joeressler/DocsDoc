using System;
using HtmlAgilityPack; // Requires HtmlAgilityPack NuGet package
using System.Linq;
using System.Collections.Generic;

namespace DocsDoc.WebScraper.Extraction
{
    /// <summary>
    /// Extracts main content from HTML, supporting all popular documentation generator formats.
    /// </summary>
    public class ContentExtractor
    {
        /// <summary>
        /// Extract main content from HTML using Html Agility Pack (HAP).
        /// Supports Sphinx, MkDocs, Docusaurus, GitBook, Jekyll, Hugo, VuePress, ReadTheDocs, and more.
        /// Adds fallback strategies and can extract metadata.
        /// </summary>
        public string Extract(string html, bool includeMetadata = true)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNode main = null!;
            // Try <main>, <article>, <body>
            main = doc.DocumentNode.SelectSingleNode("//main") ??
                   doc.DocumentNode.SelectSingleNode("//article") ??
                   doc.DocumentNode.SelectSingleNode("//body");
            // Try common doc containers if not found
            if (main == null)
            {
                string[] selectors = new[] {
                    "//*[@id='main-content']", "//*[@id='content']", "//*[@id='docs-content']", // ReadTheDocs, Sphinx
                    "//*[@class='document']", "//*[@class='content']", "//*[@class='markdown-body']",
                    "//*[@class='docs-content']", "//*[@class='md-content']", "//*[@class='post-content']",
                    "//*[@class='page-content']", "//*[@class='prose']", "//*[@class='wy-nav-content']", // Sphinx
                    "//*[@class='md-main__inner']", "//*[@class='theme-doc-markdown']", // Docusaurus
                    "//*[@class='book-page']", // GitBook
                };
                foreach (var sel in selectors)
                {
                    main = doc.DocumentNode.SelectSingleNode(sel);
                    if (main != null) break;
                }
            }
            // Fallback: all text in <body>
            if (main == null)
            {
                var body = doc.DocumentNode.SelectSingleNode("//body");
                if (body != null)
                    main = body;
            }
            if (main == null)
                return string.Empty;
            // Remove nav, sidebar, footer, header, script, style
            string[] removeTags = { ".//nav", ".//aside", ".//footer", ".//header", ".//script", ".//style", ".//div[contains(@class, 'sidebar')]", ".//div[contains(@class, 'nav')]" };
            foreach (var tag in removeTags)
            {
                var nodes = main.SelectNodes(tag);
                if (nodes != null)
                {
                    foreach (var node in nodes)
                        node.Remove();
                }
            }
            // Preserve code blocks and tables
            var codeBlocks = main.SelectNodes(".//pre|.//code");
            string codeText = codeBlocks != null ? string.Join("\n\n", codeBlocks.Select(cb => cb.InnerText)) : string.Empty;
            var tables = main.SelectNodes(".//table");
            string tableText = tables != null ? string.Join("\n\n", tables.Select(tb => tb.InnerText)) : string.Empty;
            // Extract clean text
            var text = main.InnerText;
            string result = text;
            if (!string.IsNullOrWhiteSpace(codeText))
                result += "\n\nCode blocks:\n" + codeText;
            if (!string.IsNullOrWhiteSpace(tableText))
                result += "\n\nTables:\n" + tableText;
            // Optionally extract metadata
            if (includeMetadata)
            {
                var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
                var h1s = doc.DocumentNode.SelectNodes("//h1")?.Select(h => h.InnerText.Trim()).ToList() ?? new List<string>();
                var h2s = doc.DocumentNode.SelectNodes("//h2")?.Select(h => h.InnerText.Trim()).ToList() ?? new List<string>();
                if (!string.IsNullOrWhiteSpace(title))
                    result = $"Title: {title}\n" + result;
                if (h1s.Any())
                    result = $"H1: {string.Join(" | ", h1s)}\n" + result;
                if (h2s.Any())
                    result = $"H2: {string.Join(" | ", h2s)}\n" + result;
            }
            return result.Trim();
        }
    }
} 