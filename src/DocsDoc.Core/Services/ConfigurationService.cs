using DocsDoc.Core.Models;
using System.IO;
using System.Text.Json;
using System;

namespace DocsDoc.Core.Services
{
    /// <summary>
    /// Handles loading and saving of application configuration settings from appsettings.json.
    /// </summary>
    public class ConfigurationService
    {
        private const string ConfigFileName = "appsettings.json";

        /// <summary>
        /// Loads configuration from appsettings.json, searching in multiple locations.
        /// </summary>
        public Configuration Load()
        {
            var configPath = FindConfigFile();
            if (configPath == null)
                return new Configuration();

            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            
            var modelSection = doc.RootElement.GetProperty("model");
            var modelPath = modelSection.GetProperty("path").GetString();
            var resolvedModelPath = ResolveModelPath(modelPath, configPath);
            
            var databaseSection = doc.RootElement.GetProperty("database");
            var databasePath = databaseSection.GetProperty("vectorStorePath").GetString();
            var resolvedDatabasePath = ResolveModelPath(databasePath, configPath);
            
            return new Configuration
            {
                ModelPath = resolvedModelPath,
                DatabasePath = resolvedDatabasePath,
                Backend = modelSection.GetProperty("backend").GetString(),
                AdditionalSettings = null
            };
        }

        /// <summary>
        /// Finds the appsettings.json file by searching in multiple locations.
        /// </summary>
        private string? FindConfigFile()
        {
            // Search locations in order of preference
            var searchPaths = new[]
            {
                ConfigFileName, // Current directory
                Path.Combine("..", "..", ConfigFileName), // From Desktop project to src
                Path.Combine("..", ConfigFileName), // One level up
                Path.Combine("src", ConfigFileName), // From root to src
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    LoggingService.LogInfo($"Found config file at: {Path.GetFullPath(path)}");
                    return path;
                }
            }

            LoggingService.LogError("Could not find appsettings.json file", null);
            return null;
        }

        /// <summary>
        /// Resolves the model path relative to the config file location.
        /// </summary>
        private string ResolveModelPath(string? modelPath, string configPath)
        {
            if (string.IsNullOrEmpty(modelPath))
                return string.Empty;

            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(modelPath))
                return modelPath;

            // Resolve relative to config file directory
            var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? "";
            var resolvedPath = Path.Combine(configDir, modelPath);
            
            LoggingService.LogInfo($"Resolved model path: {Path.GetFullPath(resolvedPath)}");
            return resolvedPath;
        }

        /// <summary>
        /// Saves configuration to appsettings.json (overwrites file).
        /// </summary>
        public void Save(Configuration config)
        {
            // TODO: Implement full serialization if needed
        }
    }
} 