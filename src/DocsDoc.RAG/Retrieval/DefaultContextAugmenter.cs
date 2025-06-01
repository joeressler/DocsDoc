using DocsDoc.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace DocsDoc.RAG.Retrieval
{
    /// <summary>
    /// Default implementation for building a prompt from context and user query, using Llama-3 Instruct template.
    /// </summary>
    public class DefaultContextAugmenter : IContextAugmenter
    {
        /// <summary>
        /// The global system prompt for all Llama-3 requests.
        /// </summary>
        public static string SystemPrompt { get; set; } = "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability. Always make sure to end your messages with a newline and then, I hope that helps!";

        public string BuildPrompt(string userQuery, IEnumerable<string> contextChunks)
        {
            var contextSection = contextChunks.Any()
                ? "\n\nContext:\n" + string.Join("\n---\n", contextChunks)
                : string.Empty;
            return
                "<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n" +
                SystemPrompt +
                contextSection +
                "<|eot_id|>\n" +
                "<|start_header_id|>user<|end_header_id|>\n\n" +
                userQuery +
                "<|eot_id|>\n" +
                "<|start_header_id|>assistant<|end_header_id|>\n\n";
        }
    }
} 