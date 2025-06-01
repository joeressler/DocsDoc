using System;
using System.IO;
using System.Threading.Tasks;
using DocsDoc.RAG.Processing;
using Xunit;

namespace DocsDoc.Tests
{
    public class DocumentProcessorTests
    {
        [Fact]
        public async Task ExtractTextAsync_TxtFile_Works()
        {
            var file = Path.GetTempFileName() + ".txt";
            await File.WriteAllTextAsync(file, "Hello   world!\nThis is a test.  ");
            var processor = new DefaultDocumentProcessor();
            var result = await processor.ExtractTextAsync(file);
            Assert.Equal("Hello world! This is a test.", result);
            File.Delete(file);
        }

        [Fact]
        public async Task ExtractTextAsync_MdFile_Works()
        {
            var file = Path.GetTempFileName() + ".md";
            await File.WriteAllTextAsync(file, "# Title\nSome **bold** text.");
            var processor = new DefaultDocumentProcessor();
            var result = await processor.ExtractTextAsync(file);
            Assert.Equal("# Title Some **bold** text.", result);
            File.Delete(file);
        }

        [Fact]
        public async Task ExtractTextAsync_HtmlFile_StripsTags()
        {
            var file = Path.GetTempFileName() + ".html";
            await File.WriteAllTextAsync(file, "<html><body><h1>Header</h1><p>Paragraph.</p></body></html>");
            var processor = new DefaultDocumentProcessor();
            var result = await processor.ExtractTextAsync(file);
            Assert.Equal("Header Paragraph.", result);
            File.Delete(file);
        }

        [Fact]
        public async Task ExtractTextAsync_UnsupportedFile_Throws()
        {
            var file = Path.GetTempFileName() + ".xyz";
            await File.WriteAllTextAsync(file, "data");
            var processor = new DefaultDocumentProcessor();
            await Assert.ThrowsAsync<NotSupportedException>(() => processor.ExtractTextAsync(file));
            File.Delete(file);
        }

        [Fact]
        public async Task ExtractTextAsync_MissingFile_Throws()
        {
            var processor = new DefaultDocumentProcessor();
            await Assert.ThrowsAsync<FileNotFoundException>(() => processor.ExtractTextAsync("notfound.txt"));
        }
    }
} 