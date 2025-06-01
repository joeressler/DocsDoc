using System;

namespace DocsDoc.Core.Models
{
    /// <summary>
    /// Represents a single message in a chat session, including author and content.
    /// </summary>
    public class ChatMessage
    {
        public AuthorRole Author { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// Enum for chat message author (User, Assistant, System).
    /// </summary>
    public enum AuthorRole
    {
        User,
        Assistant,
        System
    }
} 