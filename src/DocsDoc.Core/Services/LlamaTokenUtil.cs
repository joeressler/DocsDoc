using LLama;
using System;
using System.Text;

namespace DocsDoc.Core.Services
{
    /// <summary>
    /// Utility for counting tokens using LlamaSharp's tokenizer.
    /// </summary>
    public static class LlamaTokenUtil
    {
        /// <summary>
        /// Counts the number of tokens in the given text using the provided LLamaContext.
        /// </summary>
        /// <param name="text">The text to tokenize.</param>
        /// <param name="context">The LLamaContext instance.</param>
        /// <param name="addBos">Whether to add the BOS token (default: false).</param>
        /// <param name="special">Whether to allow special tokens (default: false).</param>
        /// <returns>The number of tokens in the text.</returns>
        public static int CountTokens(string text, LLamaContext context, bool addBos = false, bool special = false)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (text == null) return 0;
            var tokens = context.Tokenize(text, addBos, special);
            return tokens?.Length ?? 0;
        }
    }
} 