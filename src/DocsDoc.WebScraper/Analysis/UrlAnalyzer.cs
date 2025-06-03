using System;
using System.Text.RegularExpressions;
using DocsDoc.Core.Services;
using System.Collections.Generic;

namespace DocsDoc.WebScraper.Analysis
{
    /// <summary>
    /// Analyzes URLs to determine their type (file, docs site, API, unknown).
    /// </summary>
    public enum UrlType { File, DocsSite, Api, Unknown }

    public class UrlAnalysisResult
    {
        public UrlType Type { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> MatchedPatterns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Detects content type and determines processing strategy for URLs.
    /// </summary>
    public class UrlAnalyzer
    {
        private static readonly (UrlType type, string pattern, double confidence, string reason)[] Patterns = new[]
        {
            (UrlType.File, @"\\.(txt|md|pdf|docx|html?)$", 0.95, "File extension detected"),
            (UrlType.DocsSite, @"/docs?/|/guide/|/manual/|/reference/|/api/", 0.85, "Docs-related path segment detected"),
            (UrlType.Api, @"swagger|openapi|redoc|/api-docs?/|/swagger-ui/", 0.9, "API documentation keyword detected"),
        };

        /// <summary>
        /// Analyze a URL and return its type, confidence, and reason.
        /// </summary>
        public UrlAnalysisResult Analyze(string url)
        {
            LoggingService.LogInfo($"Analyzing URL: {url}");
            var result = new UrlAnalysisResult { Type = UrlType.Unknown, Confidence = 0.0, Reason = "No match" };
            foreach (var (type, pattern, confidence, reason) in Patterns)
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                {
                    result.Type = type;
                    result.Confidence = confidence;
                    result.Reason = reason;
                    result.MatchedPatterns.Add(pattern);
                    LoggingService.LogInfo($"URL identified as {type} (confidence: {confidence}): {url}");
                    return result;
                }
            }
            LoggingService.LogInfo($"URL type could not be determined, marked as Unknown: {url}");
            return result;
        }
    }
} 