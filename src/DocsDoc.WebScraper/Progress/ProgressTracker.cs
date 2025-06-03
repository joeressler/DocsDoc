using System;

namespace DocsDoc.WebScraper.Progress
{
    /// <summary>
    /// Tracks and reports progress for UI and logging. Supports structured progress events.
    /// </summary>
    public class ProgressTracker
    {
        /// <summary>
        /// Event raised when crawl/ingestion starts.
        /// </summary>
        public event Action<string>? Started;
        /// <summary>
        /// Event raised when a page is fetched.
        /// </summary>
        public event Action<string>? PageFetched;
        /// <summary>
        /// Event raised when an error occurs.
        /// </summary>
        public event Action<string, Exception>? Error;
        /// <summary>
        /// Event raised when crawl/ingestion is done.
        /// </summary>
        public event Action<string>? Done;

        /// <summary>
        /// Update progress status (legacy, for compatibility).
        /// </summary>
        public void Update(string status)
        {
            // TODO: Integrate with UI or logging
            Console.WriteLine($"[Progress] {status}");
        }

        /// <summary>
        /// Report crawl/ingestion start.
        /// </summary>
        public void OnStart(string url)
        {
            Console.WriteLine($"[Progress] Started: {url}");
            Started?.Invoke(url);
        }
        /// <summary>
        /// Report a page was fetched.
        /// </summary>
        public void OnPageFetched(string url)
        {
            Console.WriteLine($"[Progress] Page fetched: {url}");
            PageFetched?.Invoke(url);
        }
        /// <summary>
        /// Report an error.
        /// </summary>
        public void OnError(string url, Exception ex)
        {
            Console.WriteLine($"[Progress] Error: {url} - {ex.Message}");
            Error?.Invoke(url, ex);
        }
        /// <summary>
        /// Report crawl/ingestion done.
        /// </summary>
        public void OnDone(string summary)
        {
            Console.WriteLine($"[Progress] Done: {summary}");
            Done?.Invoke(summary);
        }
    }
} 