using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using DocsDoc.Core.Models;
using LLama;
using LLama.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocsDoc.RAG.Embedding
{
    /// <summary>
    /// Embedding service using LlamaSharp. Implements IEmbeddingProvider for embedding-only use.
    /// </summary>
    public class LlamaSharpEmbeddingService : IEmbeddingProvider, ITokenCountingProvider
    {
        private readonly ModelSettings _modelSettings;
        private readonly LLamaWeights _embeddingModel;
        private readonly LLamaContext _embeddingContext;

        /// <summary>
        /// Exposes the underlying LLamaContext for tokenization and context window info.
        /// </summary>
        public LLamaContext EmbeddingContext => _embeddingContext;

        public LlamaSharpEmbeddingService(ModelSettings modelSettings)
        {
            _modelSettings = modelSettings ?? throw new ArgumentNullException(nameof(modelSettings));
            string embeddingPath = !string.IsNullOrWhiteSpace(_modelSettings.EmbeddingModelPath) && _modelSettings.EmbeddingModelPath != _modelSettings.Path
                ? _modelSettings.EmbeddingModelPath
                : _modelSettings.Path;
            var modelParams = new ModelParams(embeddingPath!)
            {
                ContextSize = (uint)_modelSettings.ContextSize,
                GpuLayerCount = _modelSettings.GpuLayerCount,
                // Backend selection can be handled here if needed
            };
            _embeddingModel = LLamaWeights.LoadFromFile(modelParams);
            _embeddingContext = _embeddingModel.CreateContext(modelParams);
        }

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IEnumerable<string> texts)
        {
            var results = new List<float[]>();
            int i = 0;
            foreach (var text in texts)
            {
                try
                {
                    LoggingService.LogInfo($"Embedding chunk {i + 1}");
                    var embedder = new LLamaEmbedder(_embeddingModel, _embeddingContext.Params, null);
                    var embedding = await Task.Run(() => embedder.GetEmbeddings(text));
                    if (embedding.Count > 0)
                        results.Add(embedding[0]);
                    else
                        LoggingService.LogInfo($"No embedding returned for chunk {i + 1}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error embedding chunk {i + 1}", ex);
                    throw;
                }
                i++;
            }
            return results;
        }

        public int CountTokens(string text) => LlamaTokenUtil.CountTokens(text, _embeddingContext);
        public int MaxContextTokens => _modelSettings.ContextSize;
    }
} 