using DocsDoc.Core.Interfaces;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocsDoc.RAG.Processing
{
    /// <summary>
    /// Default implementation for extracting and cleaning text from documents.
    /// </summary>
    public class DefaultDocumentProcessor : IDocumentProcessor
    {
        public async Task<string> ExtractTextAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string text;
            switch (ext)
            {
                case ".txt":
                case ".md":
                    text = await File.ReadAllTextAsync(filePath);
                    break;
                case ".html":
                case ".htm":
                    text = await File.ReadAllTextAsync(filePath);
                    text = StripHtmlTags(text);
                    break;
                case ".pdf":
                    // TODO: Implement PDF extraction (stub)
                    throw new NotSupportedException("PDF extraction not implemented. Use a PDF library.");
                case ".docx":
                    // TODO: Implement DOCX extraction (stub)
                    throw new NotSupportedException("DOCX extraction not implemented. Use OpenXML SDK.");
                default:
                    throw new NotSupportedException($"Unsupported file type: {ext}");
            }
            return NormalizeWhitespace(text);
        }

        private static string StripHtmlTags(string html)
        {
            // Insert a space after every closing tag, then strip tags
            var withSpaces = Regex.Replace(html, "</[^>]+>", "$0 ");
            var noTags = Regex.Replace(withSpaces, "<.*?>", string.Empty);
            return NormalizeWhitespace(noTags);
        }

        private static string NormalizeWhitespace(string text)
        {
            // Replace multiple whitespace with single space, trim
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
} 