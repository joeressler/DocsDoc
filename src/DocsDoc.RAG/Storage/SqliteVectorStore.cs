using DocsDoc.Core.Interfaces;
using DocsDoc.Core.Services;
using DocsDoc.Core.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocsDoc.RAG.Storage
{
    /// <summary>
    /// SQLite-based vector store for embeddings.
    /// </summary>
    public class SqliteVectorStore : IVectorStore, IDisposable
    {
        private readonly DatabaseSettings _databaseSettings;
        private readonly string _dbPath;
        private readonly string _connStr;

        public SqliteVectorStore(DatabaseSettings databaseSettings)
        {
            _databaseSettings = databaseSettings ?? throw new ArgumentNullException(nameof(databaseSettings));
            _dbPath = _databaseSettings.VectorStorePath!;
            _connStr = $"Data Source={_dbPath};";
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            if (!File.Exists(_dbPath))
            {
                using var fs = File.Create(_dbPath);
            }
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS vectors (
                id TEXT PRIMARY KEY,
                text TEXT,
                embedding TEXT,
                document_source TEXT
            );";
            cmd.ExecuteNonQuery();
            
            // Check if document_source column exists, add it if not
            cmd.CommandText = "PRAGMA table_info(vectors);";
            using var reader = cmd.ExecuteReader();
            bool hasDocumentSource = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "document_source")
                {
                    hasDocumentSource = true;
                    break;
                }
            }
            reader.Close();
            
            if (!hasDocumentSource)
            {
                cmd.CommandText = "ALTER TABLE vectors ADD COLUMN document_source TEXT;";
                cmd.ExecuteNonQuery();
            }
        }

        public async Task AddAsync(IEnumerable<float[]> embeddings, IEnumerable<string> texts, IEnumerable<string> ids)
        {
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO vectors (id, text, embedding, document_source) VALUES (@id, @text, @embedding, @document_source);";
            var idParam = cmd.CreateParameter(); idParam.ParameterName = "@id";
            var textParam = cmd.CreateParameter(); textParam.ParameterName = "@text";
            var embParam = cmd.CreateParameter(); embParam.ParameterName = "@embedding";
            var docSourceParam = cmd.CreateParameter(); docSourceParam.ParameterName = "@document_source";
            cmd.Parameters.Add(idParam);
            cmd.Parameters.Add(textParam);
            cmd.Parameters.Add(embParam);
            cmd.Parameters.Add(docSourceParam);
            
            foreach (var (id, text, emb) in ids.Zip(texts, (id, text) => (id, text)).Zip(embeddings, (it, emb) => (it.id, it.text, emb)))
            {
                idParam.Value = id;
                textParam.Value = text;
                embParam.Value = JsonSerializer.Serialize(emb);
                // Extract document source from the id (format is usually "filename_chunkIndex")
                var lastUnderscoreIndex = id.LastIndexOf('_');
                var documentSource = lastUnderscoreIndex > 0 ? id.Substring(0, lastUnderscoreIndex) : id;
                docSourceParam.Value = documentSource;
                await cmd.ExecuteNonQueryAsync();
                LoggingService.LogInfo($"Stored embedding for id: {id}, source: {documentSource}");
            }
            tx.Commit();
        }

        public async Task<IReadOnlyList<(string id, float score)>> SearchAsync(float[] queryEmbedding, int topK, IEnumerable<string>? documentSources = null)
        {
            // In-memory search for now (load all, compute cosine similarity)
            var all = new List<(string id, float[] emb)>();
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            
            if (documentSources != null && documentSources.Any())
            {
                // Filter by document sources
                var sourceList = documentSources.ToList();
                var placeholders = string.Join(",", sourceList.Select((_, i) => $"@source{i}"));
                
                // Check if document_source column exists
                var schemaCmd = conn.CreateCommand();
                schemaCmd.CommandText = "PRAGMA table_info(vectors);";
                using var schemaReader = await schemaCmd.ExecuteReaderAsync();
                bool hasDocumentSource = false;
                while (await schemaReader.ReadAsync())
                {
                    if (schemaReader.GetString(1) == "document_source")
                    {
                        hasDocumentSource = true;
                        break;
                    }
                }
                schemaReader.Close();
                
                if (hasDocumentSource)
                {
                    cmd.CommandText = $"SELECT id, embedding FROM vectors WHERE document_source IN ({placeholders});";
                }
                else
                {
                    // Use ID pattern matching if no document_source column
                    var conditions = sourceList.Select((_, i) => $"id LIKE @source{i}").ToList();
                    cmd.CommandText = $"SELECT id, embedding FROM vectors WHERE {string.Join(" OR ", conditions)};";
                }
                
                for (int i = 0; i < sourceList.Count; i++)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = $"@source{i}";
                    param.Value = hasDocumentSource ? sourceList[i] : sourceList[i] + "_%";
                    cmd.Parameters.Add(param);
                }
            }
            else if (documentSources != null && !documentSources.Any())
            {
                // Empty list means no sources selected - return empty results
                LoggingService.LogInfo("No document sources specified - returning empty search results");
                return new List<(string id, float score)>();
            }
            else
            {
                // Null means search all documents (backward compatibility)
                cmd.CommandText = "SELECT id, embedding FROM vectors;";
            }
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var emb = JsonSerializer.Deserialize<float[]>(reader.GetString(1));
                if (emb != null)
                    all.Add((id, emb));
            }
            var results = all.Select(x => (x.id, CosineSimilarity(queryEmbedding, x.emb)))
                .OrderByDescending(x => x.Item2)
                .Take(topK)
                .Select(x => (x.id, x.Item2))
                .ToList();
            LoggingService.LogInfo($"Search returned {results.Count} results from {(documentSources?.Count() ?? 0)} filtered sources.");
            return results;
        }

        public async Task<string?> GetTextByIdAsync(string id)
        {
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT text FROM vectors WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result as string;
        }

        public async Task<IReadOnlyList<string>> GetAllDocumentSourcesAsync()
        {
            var sources = new List<string>();
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();
            
            // Check if document_source column exists
            using var schemaCmd = conn.CreateCommand();
            schemaCmd.CommandText = "PRAGMA table_info(vectors);";
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();
            bool hasDocumentSource = false;
            while (await schemaReader.ReadAsync())
            {
                if (schemaReader.GetString(1) == "document_source")
                {
                    hasDocumentSource = true;
                    break;
                }
            }
            schemaReader.Close();
            
            using var cmd = conn.CreateCommand();
            if (hasDocumentSource)
            {
                // Use the document_source column if it exists
                cmd.CommandText = "SELECT DISTINCT document_source FROM vectors WHERE document_source IS NOT NULL ORDER BY document_source;";
            }
            else
            {
                // Extract document sources from IDs (format is usually "filename_chunkIndex")
                cmd.CommandText = @"SELECT DISTINCT 
                    CASE 
                        WHEN INSTR(id, '_') > 0 THEN SUBSTR(id, 1, INSTR(id, '_') - 1)
                        ELSE id 
                    END as document_source 
                    FROM vectors 
                    ORDER BY document_source;";
            }
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var source = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(source))
                    sources.Add(source);
            }
            LoggingService.LogInfo($"Found {sources.Count} document sources in vector store.");
            return sources;
        }

        public async Task DeleteDocumentAsync(string documentSource)
        {
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();
            
            // Check if document_source column exists
            using var schemaCmd = conn.CreateCommand();
            schemaCmd.CommandText = "PRAGMA table_info(vectors);";
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();
            bool hasDocumentSource = false;
            while (await schemaReader.ReadAsync())
            {
                if (schemaReader.GetString(1) == "document_source")
                {
                    hasDocumentSource = true;
                    break;
                }
            }
            schemaReader.Close();
            
            using var cmd = conn.CreateCommand();
            if (hasDocumentSource)
            {
                // Use the document_source column if it exists
                cmd.CommandText = "DELETE FROM vectors WHERE document_source = @document_source;";
                cmd.Parameters.AddWithValue("@document_source", documentSource);
            }
            else
            {
                // Delete based on ID pattern (format is usually "filename_chunkIndex")
                cmd.CommandText = "DELETE FROM vectors WHERE id LIKE @id_pattern;";
                cmd.Parameters.AddWithValue("@id_pattern", documentSource + "_%");
            }
            
            var deletedRows = await cmd.ExecuteNonQueryAsync();
            LoggingService.LogInfo($"Deleted {deletedRows} embeddings for document source: {documentSource}");
        }

        public async Task DeleteDocumentGroupAsync(string groupName)
        {
            using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync();

            // Check if document_source column exists
            using var schemaCmd = conn.CreateCommand();
            schemaCmd.CommandText = "PRAGMA table_info(vectors);";
            using var schemaReader = await schemaCmd.ExecuteReaderAsync();
            bool hasDocumentSource = false;
            while (await schemaReader.ReadAsync())
            {
                if (schemaReader.GetString(1) == "document_source")
                {
                    hasDocumentSource = true;
                    break;
                }
            }
            schemaReader.Close();

            using var cmd = conn.CreateCommand();
            if (hasDocumentSource)
            {
                // Delete all where document_source starts with 'groupName|'
                cmd.CommandText = "DELETE FROM vectors WHERE document_source LIKE @group_prefix;";
                cmd.Parameters.AddWithValue("@group_prefix", groupName + "|%");
            }
            else
            {
                // Delete based on ID pattern
                cmd.CommandText = "DELETE FROM vectors WHERE id LIKE @id_pattern;";
                cmd.Parameters.AddWithValue("@id_pattern", groupName + "|%");
            }

            var deletedRows = await cmd.ExecuteNonQueryAsync();
            LoggingService.LogInfo($"Deleted {deletedRows} embeddings for document group: {groupName}");
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0f;
            float dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-8));
        }

        // Implement IDisposable for future extensibility (no-op for now)
        public void Dispose()
        {
            // No persistent connection to dispose, but allows for future pooling/extension
        }
    }
} 