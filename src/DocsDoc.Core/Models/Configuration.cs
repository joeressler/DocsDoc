using System.Collections.Generic;

namespace DocsDoc.Core.Models
{
    /// <summary>
    /// Represents application and model configuration settings.
    /// </summary>
    public class Configuration
    {
        public string? ModelPath { get; set; }
        public string? Backend { get; set; }
        public string? DatabasePath { get; set; }
        public Dictionary<string, string>? AdditionalSettings { get; set; }
    }
} 