using System;
using System.Text.RegularExpressions;
using DocsDoc.Core.Services;

namespace DocsDoc.WebScraper.Analysis
{
    /// <summary>
    /// Analyzes URLs to determine their type (file, docs site, API, unknown).
    /// </summary>
    public enum UrlType { File, DocsSite, Api, Unknown }

    /// <summary>
    /// Detects content type and determines processing strategy for URLs.
    /// </summary>
    public class UrlAnalyzer
    {
        /// <summary>
        /// Analyze a URL and return its type.
        /// </summary>
        public UrlType Analyze(string url)
        {
            LoggingService.LogInfo($"Analyzing URL: {url}");
            
            if (Regex.IsMatch(url, @"\.(txt|md|pdf|docx|html?)$", RegexOptions.IgnoreCase))
            {
                LoggingService.LogInfo($"URL identified as File type: {url}");
                return UrlType.File;
            }
            
            if (Regex.IsMatch(url, @"/docs?/|/guide/|/manual/|/reference/|/api/", RegexOptions.IgnoreCase))
            {
                LoggingService.LogInfo($"URL identified as DocsSite type: {url}");
                return UrlType.DocsSite;
            }
            
            if (Regex.IsMatch(url, @"swagger|openapi|redoc", RegexOptions.IgnoreCase))
            {
                LoggingService.LogInfo($"URL identified as Api type: {url}");
                return UrlType.Api;
            }
            
            LoggingService.LogInfo($"URL type could not be determined, marked as Unknown: {url}");
            return UrlType.Unknown;
        }
    }
} 