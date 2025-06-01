using System;
using System.Collections.Generic;

namespace DocsDoc.Core.Models
{
    /// <summary>
    /// Represents a document ingested into the system, including source and metadata.
    /// </summary>
    public class Document
    {
        public Guid Id { get; set; }
        public string Source { get; set; } // File path or URL
        public string Content { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
    }
} 