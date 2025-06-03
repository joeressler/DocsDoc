using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DocsDoc.WebScraper.Deduplication
{
    /// <summary>
    /// Deduplicates content by hash.
    /// </summary>
    public class ContentDeduplicator
    {
        private readonly HashSet<string> _hashes = new HashSet<string>();
        private readonly HashSet<string> _urls = new HashSet<string>();
        private readonly string? _hashCacheFile;

        /// <summary>
        /// Optionally load hashes from disk for persistent deduplication.
        /// </summary>
        public ContentDeduplicator(string? hashCacheFile = null)
        {
            _hashCacheFile = hashCacheFile;
            if (!string.IsNullOrWhiteSpace(_hashCacheFile) && System.IO.File.Exists(_hashCacheFile))
            {
                foreach (var line in System.IO.File.ReadAllLines(_hashCacheFile))
                    _hashes.Add(line);
            }
        }

        /// <summary>
        /// Returns true if content or URL is duplicate, false otherwise. Optionally persists hash.
        /// </summary>
        public bool IsDuplicate(string content, string? url = null)
        {
            using var sha = SHA256.Create();
            var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
            if (_hashes.Contains(hash))
                return true;
            if (!string.IsNullOrWhiteSpace(url) && _urls.Contains(url))
                return true;
            _hashes.Add(hash);
            if (!string.IsNullOrWhiteSpace(url))
                _urls.Add(url);
            if (!string.IsNullOrWhiteSpace(_hashCacheFile))
                System.IO.File.AppendAllLines(_hashCacheFile, new[] { hash });
            return false;
        }
    }
} 