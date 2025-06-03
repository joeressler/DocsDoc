using System;
using System.Linq;
using DocsDoc.RAG.Processing;
using Xunit;

namespace DocsDoc.Tests
{
    public class TextChunkerTests
    {
        [Fact]
        public void ChunkText_NoOverlap_Works()
        {
            var chunker = new DefaultTextChunker(new DocsDoc.Core.Models.RagSettings());
            string text = string.Join(" ", Enumerable.Range(1, 10));
            var chunks = chunker.ChunkText(text, 4, 0);
            Assert.Equal(3, chunks.Count);
            Assert.Equal("1 2 3 4", chunks[0]);
            Assert.Equal("5 6 7 8", chunks[1]);
            Assert.Equal("9 10", chunks[2]);
        }

        [Fact]
        public void ChunkText_WithOverlap_Works()
        {
            var chunker = new DefaultTextChunker(new DocsDoc.Core.Models.RagSettings());
            string text = string.Join(" ", Enumerable.Range(1, 8));
            var chunks = chunker.ChunkText(text, 4, 2);
            Assert.Equal(3, chunks.Count);
            Assert.Equal("1 2 3 4", chunks[0]);
            Assert.Equal("3 4 5 6", chunks[1]);
            Assert.Equal("5 6 7 8", chunks[2]);
        }

        [Fact]
        public void ChunkText_ChunkSizeLargerThanText_OneChunk()
        {
            var chunker = new DefaultTextChunker(new DocsDoc.Core.Models.RagSettings());
            string text = "one two three";
            var chunks = chunker.ChunkText(text, 10, 0);
            Assert.Single(chunks);
            Assert.Equal("one two three", chunks[0]);
        }

        [Fact]
        public void ChunkText_InvalidParameters_Throws()
        {
            var chunker = new DefaultTextChunker(new DocsDoc.Core.Models.RagSettings());
            Assert.Throws<ArgumentOutOfRangeException>(() => chunker.ChunkText("text", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => chunker.ChunkText("text", 2, -1));
        }
    }
} 