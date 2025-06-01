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

        /// <summary>
        /// Returns true if content is duplicate, false otherwise.
        /// </summary>
        public bool IsDuplicate(string content)
        {
            using var sha = SHA256.Create();
            var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
            if (_hashes.Contains(hash))
                return true;
            _hashes.Add(hash);
            return false;
        }
    }
} 