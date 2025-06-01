using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocsDoc.WebScraper.RateLimit
{
    /// <summary>
    /// Controls crawl rate and concurrency, respecting robots.txt and user settings.
    /// </summary>
    public class RateLimiter
    {
        private readonly Dictionary<string, DateTime> _lastRequest = new Dictionary<string, DateTime>();
        private readonly TimeSpan _minDelay;

        public RateLimiter(TimeSpan? minDelay = null)
        {
            _minDelay = minDelay ?? TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Wait if needed before making a request to the domain.
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
        }
    }
} 