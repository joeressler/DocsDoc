namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Provides token counting and context window info for chunk validation.
    /// </summary>
    public interface ITokenCountingProvider
    {
        /// <summary>
        /// Counts the number of tokens in the given text.
        /// </summary>
        int CountTokens(string text);

        /// <summary>
        /// The maximum number of tokens allowed in a chunk (context window).
        /// </summary>
        int MaxContextTokens { get; }
    }
} 