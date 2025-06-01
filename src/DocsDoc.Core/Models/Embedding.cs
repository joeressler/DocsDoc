using System;

namespace DocsDoc.Core.Models
{
    /// <summary>
    /// Represents a vector embedding for a document chunk or query.
    /// </summary>
    public class Embedding
    {
        public Guid Id { get; set; }
        public float[] Vector { get; set; }
        public Guid DocumentId { get; set; }
        public int ChunkIndex { get; set; }
    }
} 