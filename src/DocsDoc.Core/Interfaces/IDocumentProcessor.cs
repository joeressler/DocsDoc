using System.Threading.Tasks;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Extracts and cleans text from a document file.
    /// </summary>
    public interface IDocumentProcessor
    {
        /// <summary>
        /// Extracts and cleans text from a document.
        /// </summary>
        /// <param name="filePath">Path to the document file.</param>
        /// <returns>Cleaned plain text.</returns>
        Task<string> ExtractTextAsync(string filePath);
    }
} 