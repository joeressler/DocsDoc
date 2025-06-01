using System;
using System.IO;
using System.Threading.Tasks;
using DocsDoc.RAG;
using DocsDoc.Core.Services;
using Xunit;

namespace DocsDoc.Tests
{
    public class RagOrchestratorTests
    {
        private static string? FindModelPath(string fileName)
        {
            // Try current directory and up to 3 parent directories
            string[] searchDirs = new[]
            {
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "models"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "..", "models")
            };
            foreach (var dir in searchDirs)
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        [Fact]
        public async Task RagOrchestrator_EndToEnd_Works()
        {
            LoggingService.Configure();
            string modelFile = "Meta-Llama-3-8B-Instruct.Q6_K.gguf";
            var modelPath = FindModelPath(modelFile);
            if (modelPath == null)
            {
                LoggingService.LogInfo($"Model file not found: {modelFile}. Skipping test.");
                return;
            }
            string dbPath = Path.GetTempFileName() + ".sqlite";
            string docPath = Path.GetTempFileName() + ".txt";
            string docText = "The capital of France is Paris. Paris is known for the Eiffel Tower.";
            await File.WriteAllTextAsync(docPath, docText);

            var rag = new RagOrchestrator(modelPath, dbPath);
            await rag.IngestDocumentAsync(docPath, chunkSize: 10, overlap: 0);
            LoggingService.LogInfo("Document ingested.");

            string query = "What is the capital of France?";
            string answer = await rag.QueryAsync(query, topK: 2);
            LoggingService.LogInfo($"RAG answer: {answer}");
            Assert.False(string.IsNullOrWhiteSpace(answer));

            rag.Dispose();
            File.Delete(docPath);
            File.Delete(dbPath);
        }
    }
} 