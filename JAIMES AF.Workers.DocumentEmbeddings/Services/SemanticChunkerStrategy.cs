using Microsoft.Extensions.Logging;
using SemanticChunkerNET;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Models;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Configuration;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

/// <summary>
/// Chunking strategy using SemanticChunker.NET for embedding-driven, context-aware text chunking
/// </summary>
public class SemanticChunkerStrategy(
    SemanticChunker semanticChunker,
    ILogger<SemanticChunkerStrategy> logger) : ITextChunkingStrategy
{
    public IEnumerable<TextChunk> ChunkText(string text, string sourceDocumentId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        // SemanticChunker.NET requires async, but our interface is synchronous
        // We'll use GetAwaiter().GetResult() to bridge the gap
        // In a production scenario, you might want to make ITextChunkingStrategy async
        IList<SemanticChunkerNET.Chunk> semanticChunks;
        try
        {
            semanticChunks = semanticChunker
                .CreateChunksAsync(text)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to chunk text using SemanticChunker for document {DocumentId}", sourceDocumentId);
            throw;
        }

        logger.LogDebug("SemanticChunker produced {Count} chunks for document {DocumentId}", 
            semanticChunks.Count, sourceDocumentId);

        for (int i = 0; i < semanticChunks.Count; i++)
        {
            SemanticChunkerNET.Chunk semanticChunk = semanticChunks[i];

            yield return new TextChunk
            {
                Id = GenerateChunkId(sourceDocumentId, i),
                Text = semanticChunk.Text,
                Index = i,
                SourceDocumentId = sourceDocumentId
            };
        }
    }

    private static string GenerateChunkId(string sourceDocumentId, int chunkIndex)
    {
        return $"{sourceDocumentId}_chunk_{chunkIndex}";
    }
}

