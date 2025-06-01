using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Retrieves relevant text chunks for a user query.
    /// </summary>
    public interface IRetrievalEngine
    {
        /// <summary>
        /// Retrieve top-K relevant text chunks for a query.
        /// </summary>
        /// <param name="query">User query</param>
        /// <param name="topK">Number of top results to return</param>
        /// <param name="documentSources">Optional filter to limit retrieval to specific document sources</param>
        Task<IReadOnlyList<string>> RetrieveRelevantChunksAsync(string query, int topK, IEnumerable<string>? documentSources = null);
    }
} 