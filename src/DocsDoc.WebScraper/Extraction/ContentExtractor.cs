using System;
using HtmlAgilityPack; // Requires HtmlAgilityPack NuGet package
using System.Linq;

namespace DocsDoc.WebScraper.Extraction
{
    /// <summary>
    /// Extracts main content from HTML, supporting all popular documentation generator formats.
    /// </summary>
    public class ContentExtractor
    {
        /// <summary>
        /// Extract main content from HTML using Html Agility Pack (HAP).
        /// Supports Sphinx, MkDocs, Docusaurus, GitBook, Jekyll, Hugo, VuePress, and more.
        /// </summary>
        public string Extract(string html)
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
                    "//*[@class='document']", "//*[@class='content']", "//*[@class='markdown-body']",
                    "//*[@class='docs-content']", "//*[@class='md-content']", "//*[@class='post-content']",
                    "//*[@class='page-content']", "//*[@class='prose']"
                };
                foreach (var sel in selectors)
                {
                    main = doc.DocumentNode.SelectSingleNode(sel);
                    if (main != null) break;
                }
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
            return result.Trim();
        }
    }
} 