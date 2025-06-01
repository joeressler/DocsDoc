using System;
using System.IO;
using System.Threading.Tasks;
using DocsDoc.RAG;
using DocsDoc.Core.Services;
using LLama.Common;
using Xunit;
using DocsDoc.WebScraper.Analysis;
using DocsDoc.WebScraper.Crawling;
using DocsDoc.WebScraper.Extraction;
using System.Collections.Generic;
using System.Linq;

namespace DocsDoc.Tests;

public class UnitTest1
{
    private static string? FindModelPath(string fileName)
    {
        // Log the current working directory for debugging
        LoggingService.LogInfo($"Test working directory: {Directory.GetCurrentDirectory()}");

        // Try all likely locations
        string[] searchDirs = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "models"),
        };
        foreach (var dir in searchDirs)
        {
            var candidate = Path.GetFullPath(Path.Combine(dir, fileName));
            LoggingService.LogInfo($"Checking for model at: {candidate}");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
    /*
    [Fact]
    public void Test1()
    {

    }

    [Fact]
    public async Task LlamaSharpInferenceService_BasicGeneration_Works()
    {
        LoggingService.Configure();
        LoggingService.LogInfo("Starting LlamaSharpInferenceService_BasicGeneration_Works test");
        string modelFile = "Meta-Llama-3-8B-Instruct.Q6_K.gguf";
        string? modelPath = FindModelPath(modelFile);
        if (modelPath == null)
        {
            LoggingService.LogInfo($"Test skipped: model file not found in any known models folder (searched for {modelFile})");
            return;
        }
        LoggingService.LogInfo($"Using model file: {modelPath}");
        var modelParams = new ModelParams(modelPath) { ContextSize = 64, GpuLayerCount = 32 };
        try
        {
            using var service = new LlamaSharpInferenceService(modelPath, modelParams);
            string prompt = "<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n" +
                            "You are a helpful, smart, kind, and efficient AI assistant. You always fulfill the user's requests to the best of your ability.<|eot_id|>\n" +
                            "<|start_header_id|>user<|end_header_id|>\n\n" +
                            "What is the capital of France?<|eot_id|>\n" +
                            "<|start_header_id|>assistant<|end_header_id|>\n\n";
            var inferenceParams = new InferenceParams
            {
                MaxTokens = 64,
                AntiPrompts = new List<string> { "<|eot_id|>" }
            };
            string result = await service.GenerateAsync(prompt, inferenceParams);
            LoggingService.LogInfo($"Test result: {result}");
            Assert.False(string.IsNullOrWhiteSpace(result));
            LoggingService.LogInfo(result);
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Test failed with exception", ex);
            throw;
        }
    }
    */

    [Fact]
    public void UrlAnalyzer_DetectsTypes()
    {
        var analyzer = new UrlAnalyzer();
        Assert.Equal(UrlType.File, analyzer.Analyze("https://site.com/file.txt"));
        Assert.Equal(UrlType.DocsSite, analyzer.Analyze("https://site.com/docs/intro"));
        Assert.Equal(UrlType.Api, analyzer.Analyze("https://site.com/swagger"));
        Assert.Equal(UrlType.Unknown, analyzer.Analyze("https://site.com/other"));
    }

    [Fact]
    public void WebCrawler_ExtractLinks_Works()
    {
        var crawler = new WebCrawler();
        string html = @"<a href=""https://site.com/page1"">Page1</a> <a href='page2'>Page2</a> <a href=""/page3"">Page3</a>";
        var links = crawler.GetType().GetMethod("ExtractLinks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(crawler, new object[] { html, "site.com" }) as IEnumerable<string>;
        var linkList = links.ToList();
        Assert.Contains("https://site.com/page1", linkList);
        Assert.Contains("https://site.com/page2", linkList);
        Assert.Contains("https://site.com/page3", linkList);
    }

    [Fact]
    public void ContentExtractor_ExtractsMainContent()
    {
        var extractor = new ContentExtractor();
        string html = @"<html><body><main><h1>Title</h1><p>Doc text.</p><pre>code block</pre><table><tr><td>cell</td></tr></table></main></body></html>";
        var content = extractor.Extract(html);
        Assert.Contains("Title", content);
        Assert.Contains("Doc text.", content);
        Assert.Contains("code block", content);
        Assert.Contains("cell", content);
    }
}

public class WebScraperUnitTests
{
    [Fact]
    public void UrlAnalyzer_DetectsTypes()
    {
        var analyzer = new UrlAnalyzer();
        Assert.Equal(UrlType.File, analyzer.Analyze("https://site.com/file.txt"));
        Assert.Equal(UrlType.DocsSite, analyzer.Analyze("https://site.com/docs/intro"));
        Assert.Equal(UrlType.Api, analyzer.Analyze("https://site.com/swagger"));
        Assert.Equal(UrlType.Unknown, analyzer.Analyze("https://site.com/other"));
    }

    [Fact]
    public void WebCrawler_ExtractLinks_Works()
    {
        var crawler = new WebCrawler();
        string html = @"<a href=""https://site.com/page1"">Page1</a> <a href='page2'>Page2</a> <a href=""/page3"">Page3</a>";
        var links = crawler.GetType().GetMethod("ExtractLinks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(crawler, new object[] { html, "site.com" }) as IEnumerable<string>;
        var linkList = links.ToList();
        Assert.Contains("https://site.com/page1", linkList);
        Assert.Contains("https://site.com/page2", linkList);
        Assert.Contains("https://site.com/page3", linkList);
    }

    [Fact]
    public void ContentExtractor_ExtractsMainContent()
    {
        var extractor = new ContentExtractor();
        string html = @"<html><body><main><h1>Title</h1><p>Doc text.</p><pre>code block</pre><table><tr><td>cell</td></tr></table></main></body></html>";
        var content = extractor.Extract(html);
        Assert.Contains("Title", content);
        Assert.Contains("Doc text.", content);
        Assert.Contains("code block", content);
        Assert.Contains("cell", content);
    }
}