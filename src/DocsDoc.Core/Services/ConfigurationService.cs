using DocsDoc.Core.Models;
using System.IO;
using System.Text.Json;
using System;
using System.Text.Json.Serialization;
using System.Linq;

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
        /// Paths within the configuration are resolved to be absolute.
        /// </summary>
        public Configuration Load()
        {
            var configPath = FindConfigFile();
            Configuration config;

            if (configPath == null || !File.Exists(configPath))
            {
                LoggingService.LogInfo($"Configuration file '{ConfigFileName}' not found. Using default configuration.");
                config = new Configuration(); // Return default configuration
            }
            else
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true, // Should not be needed if casing is consistent
                        // Converters can be added here if needed for complex types
                    };
                    config = JsonSerializer.Deserialize<Configuration>(json, options) ?? new Configuration();
                    LoggingService.LogInfo($"Configuration loaded from: {configPath}");
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error loading configuration from '{configPath}'. Using default configuration.", ex);
                    config = new Configuration(); // Return default on error
                }
            }

            // Resolve paths after loading, relative to the config file's directory (if found)
            var configFileDirectory = configPath != null ? Path.GetDirectoryName(Path.GetFullPath(configPath)) : AppContext.BaseDirectory;
            
            if (config.Model != null)
            {
                config.Model.Path = ResolvePath(config.Model.Path, configFileDirectory);
                config.Model.EmbeddingModelPath = ResolvePath(config.Model.EmbeddingModelPath, configFileDirectory);
            }
            if (config.Database != null)
            {
                config.Database.VectorStorePath = ResolvePath(config.Database.VectorStorePath, configFileDirectory);
            }
            if (config.WebScraper != null)
            {
                config.WebScraper.CachePath = ResolvePath(config.WebScraper.CachePath, configFileDirectory);
            }
            if (config.Logging != null)
            {
                config.Logging.LogFilePath = ResolvePath(config.Logging.LogFilePath, configFileDirectory);
            }

            return config;
        }

        /// <summary>
        /// Saves configuration to appsettings.json (overwrites file).
        /// </summary>
        public void Save(Configuration config)
        {
            var configPath = FindConfigFile();
            if (configPath == null)
            {
                // If the config file doesn't exist, try to create it in a default location.
                // For simplicity, we'll try to save it in the application's base directory.
                // More sophisticated logic might be needed for different deployment scenarios.
                configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
                LoggingService.LogInfo($"Config file not found. Attempting to save to default location: {configPath}");
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
                };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
                LoggingService.LogInfo($"Configuration saved to: {configPath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save configuration to '{configPath}'", ex);
                // Depending on the application, might want to throw or handle this more gracefully.
            }
        }

        /// <summary>
        /// Finds the appsettings.json file by searching in multiple locations.
        /// </summary>
        private string? FindConfigFile()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var appBaseDir = AppContext.BaseDirectory;

            var searchPaths = new[]
            {
                // Relative to current working directory
                ConfigFileName,
                Path.Combine(currentDir, ConfigFileName),
                Path.Combine(currentDir, "..", ConfigFileName),          // Project Root (e.g., src/DocsDoc.Desktop/)
                Path.Combine(currentDir, "..", "..", ConfigFileName),      // Solution Root (e.g., src/)
                Path.Combine(currentDir, "..", "..", "..", ConfigFileName),// Workspace Root (e.g., GitHub/DocsDoc/)
                
                // Relative to AppContext.BaseDirectory (often bin/Debug/netX.Y)
                Path.Combine(appBaseDir, ConfigFileName),
                Path.Combine(appBaseDir, "..", ConfigFileName),
                Path.Combine(appBaseDir, "..", "..", ConfigFileName),
                Path.Combine(appBaseDir, "..", "..", "..", ConfigFileName),
                Path.Combine(appBaseDir, "..", "..", "..", "..", ConfigFileName), // Deeper for some project structures

                // Specific known structures from workspace root (assuming 'src' convention)
                Path.Combine(currentDir, "..", "..", "src", ConfigFileName), // If CWD is a project inside src
                Path.Combine(appBaseDir, "..", "..", "..", "src", ConfigFileName) // If AppBase is deep in bin
            };

            foreach (var path in searchPaths.Distinct()) // Use Distinct to avoid redundant checks if paths resolve the same
            {
                var fullPath = Path.GetFullPath(path); // Normalize the path
                if (File.Exists(fullPath))
                {
                    LoggingService.LogInfo($"Found config file at: {fullPath}");
                    return fullPath;
                }
            }
            
            // Fallback to trying to find it in the root of the drive C: during development for convenience, REMOVE for production
            // string devFallbackPath = Path.Combine(Path.GetPathRoot(currentDir) ?? "C:\", "Users", Environment.UserName, "OneDrive", "Documents", "GitHub", "DocsDoc", ConfigFileName);
            // if(File.Exists(devFallbackPath))
            // {
            //     LoggingService.LogInfo($"Found config file at DEVELOPMENT FALLBACK: {devFallbackPath}");
            //     return devFallbackPath;
            // }
            
            // A more specific attempt to find it assuming workspace root is a few levels up from AppContext.BaseDirectory
            // and appsettings.json is at that root or in src.
            // This is heuristic and might need adjustment based on actual deployment/dev structure.
            var workspaceRootGuess = Path.GetFullPath(Path.Combine(appBaseDir, "..", "..", "..")); // Common for bin/Debug/netX.Y
            var potentialPathsFromWorkspace = new[] {
                Path.Combine(workspaceRootGuess, ConfigFileName),
                Path.Combine(workspaceRootGuess, "src", ConfigFileName)
            };
            foreach (var path in potentialPathsFromWorkspace)
            {
                if (File.Exists(path))
                {
                    LoggingService.LogInfo($"Found config file via workspace guess at: {path}");
                    return path;
                }
            }


            LoggingService.LogInfo($"Could not find '{ConfigFileName}' in standard search paths.");
            return null;
        }

        /// <summary>
        /// Resolves a given path relative to a base directory if it's not already absolute.
        /// </summary>
        /// <param name="pathValue">The path string to resolve.</param>
        /// <param name="baseDirectoryPath">The base directory to resolve against.</param>
        /// <returns>An absolute path, or the original pathValue if null/empty or already absolute.</returns>
        private string? ResolvePath(string? pathValue, string? baseDirectoryPath)
        {
            if (string.IsNullOrEmpty(pathValue) || string.IsNullOrEmpty(baseDirectoryPath))
                return pathValue;

            // If it's already an absolute path, return as-is
            if (Path.IsPathRooted(pathValue))
                return pathValue;

            // Resolve relative to the base directory
            var resolvedPath = Path.GetFullPath(Path.Combine(baseDirectoryPath, pathValue));
            // LoggingService.LogInfo($"Resolved path: Original='{pathValue}', Base='{baseDirectoryPath}', Resolved='{resolvedPath}'");
            return resolvedPath;
        }
    }
} 