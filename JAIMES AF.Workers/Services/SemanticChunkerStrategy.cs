using MattEland.Jaimes.ServiceDefinitions.Configuration;
using MattEland.Jaimes.ServiceDefinitions.Models;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.Logging;
using SemanticChunkerNET;

namespace MattEland.Jaimes.Workers.Services;

/// <summary>
/// Chunking strategy using SemanticChunker.NET for embedding-driven, context-aware text chunking
/// </summary>
public class SemanticChunkerStrategy(
    SemanticChunker semanticChunker,
    DocumentChunkingOptions options,
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

        int chunkIndex = 0;
        int filteredCount = 0;
        for (int i = 0; i < semanticChunks.Count; i++)
        {
            SemanticChunkerNET.Chunk semanticChunk = semanticChunks[i];

            // Filter out chunks that are too short (MinChunkChars)
            if (semanticChunk.Text.Length < options.MinChunkChars)
            {
                filteredCount++;
                logger.LogDebug("Filtered out chunk {Index} for document {DocumentId} (length {Length} < MinChunkChars {MinChars})", 
                    i, sourceDocumentId, semanticChunk.Text.Length, options.MinChunkChars);
                continue;
            }

            // Extract embedding from SemanticChunker.NET Chunk
            float[]? embedding = null;
            if (semanticChunk.Embedding != null)
            {
                // Embedding<float> from Microsoft.Extensions.AI has a Vector property
                embedding = semanticChunk.Embedding.Vector.ToArray();
            }

            yield return new TextChunk
            {
                Id = GenerateChunkId(sourceDocumentId, chunkIndex),
                Text = semanticChunk.Text,
                Index = chunkIndex,
                SourceDocumentId = sourceDocumentId,
                Embedding = embedding
            };
            
            chunkIndex++;
        }

        if (filteredCount > 0)
        {
            logger.LogInformation("Filtered out {FilteredCount} chunks shorter than {MinChunkChars} characters for document {DocumentId}", 
                filteredCount, options.MinChunkChars, sourceDocumentId);
        }
    }

    private static string GenerateChunkId(string sourceDocumentId, int chunkIndex)
    {
        return $"{sourceDocumentId}_chunk_{chunkIndex}";
    }
}
