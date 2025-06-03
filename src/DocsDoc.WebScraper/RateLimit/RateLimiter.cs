using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocsDoc.Core.Models;
using System.Linq;

namespace DocsDoc.WebScraper.RateLimit
{
    /// <summary>
    /// Controls crawl rate and concurrency, respecting robots.txt and user settings.
    /// </summary>
    public class RateLimiter
    {
        private readonly Dictionary<string, DateTime> _lastRequest = new Dictionary<string, DateTime>();
        private readonly TimeSpan _minDelay;
        private readonly WebScraperSettings _settings;
        private readonly string? _stateFile;

        /// <summary>
        /// Optionally load last-request times from disk for persistent politeness.
        /// </summary>
        public RateLimiter(WebScraperSettings settings, string? stateFile = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _minDelay = TimeSpan.FromSeconds(_settings.RateLimitSeconds);
            _stateFile = stateFile;
            if (!string.IsNullOrWhiteSpace(_stateFile) && System.IO.File.Exists(_stateFile))
            {
                foreach (var line in System.IO.File.ReadAllLines(_stateFile))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2 && DateTime.TryParse(parts[1], out var dt))
                        _lastRequest[parts[0]] = dt;
                }
            }
        }

        /// <summary>
        /// Wait if needed before making a request to the domain. Saves last-request times to disk if enabled.
        /// </summary>
        public async Task WaitIfNeeded(string domain)
        {
            if (_lastRequest.TryGetValue(domain, out var last))
            {
                var elapsed = DateTime.UtcNow - last;
                if (elapsed < _minDelay)
                    await Task.Delay(_minDelay - elapsed);
            }
            _lastRequest[domain] = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(_stateFile))
            {
                try
                {
                    System.IO.File.WriteAllLines(_stateFile, _lastRequest.Select(kvp => $"{kvp.Key}\t{kvp.Value:o}"));
                }
                catch { /* Ignore errors */ }
            }
        }
    }
} 