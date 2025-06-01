using System;

namespace DocsDoc.WebScraper.Progress
{
    /// <summary>
    /// Tracks and reports progress for UI.
    /// </summary>
    public class ProgressTracker
    {
        /// <summary>
        /// Update progress status (stub for now).
        /// </summary>
        public void Update(string status)
        {
            // TODO: Integrate with UI or logging
            Console.WriteLine($"[Progress] {status}");
        }
    }
} 