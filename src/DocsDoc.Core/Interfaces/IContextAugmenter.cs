using System.Collections.Generic;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Builds a prompt for the LLM from user query and context chunks.
    /// </summary>
    public interface IContextAugmenter
    {
        /// <summary>
        /// Build a prompt from user query and context.
        /// </summary>
        /// <param name="userQuery">The user's query.</param>
        /// <param name="contextChunks">Relevant context chunks.</param>
        /// <returns>Prompt for LLM.</returns>
        string BuildPrompt(string userQuery, IEnumerable<string> contextChunks);
    }
} 