using MattEland.Jaimes.Workers.DocumentChunking.Configuration;
using MattEland.Jaimes.Workers.DocumentChunking.Models;
using SemanticSlicer;
using SemanticSlicer.Models;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

/// <summary>
/// Chunking strategy using SemanticSlicer for text-based chunking without requiring embeddings
/// </summary>
public class SemanticSlicerStrategy(
    DocumentChunkingOptions options,
    ILogger<SemanticSlicerStrategy> logger) : ITextChunkingStrategy
{
    public IEnumerable<TextChunk> ChunkText(string text, string sourceDocumentId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        // Map separator string to Separator array
        Separator[] separators = options.SemanticSlicerSeparators switch
        {
            "Markdown" => Separators.Markdown,
            "Html" => Separators.Html,
            "Text" => Separators.Text,
            _ => Separators.Text // Default to Text separators
        };

        SlicerOptions slicerOptions = new()
        {
            MaxChunkTokenCount = options.SemanticSlicerMaxChunkTokenCount,
            Separators = separators,
            StripHtml = options.SemanticSlicerStripHtml
        };

        Slicer slicer = new(slicerOptions);
        
        IList<DocumentChunk> documentChunks;
        try
        {
            documentChunks = slicer.GetDocumentChunks(text).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to chunk text using SemanticSlicer for document {DocumentId}", sourceDocumentId);
            throw;
        }

        logger.LogDebug("SemanticSlicer produced {Count} chunks for document {DocumentId}", 
            documentChunks.Count, sourceDocumentId);

        int chunkIndex = 0;
        int filteredCount = 0;
        foreach (DocumentChunk documentChunk in documentChunks)
        {
            // DocumentChunk has a Content property (not Text)
            string chunkText = documentChunk.Content ?? string.Empty;
            
            // Filter out chunks that are too short (MinChunkChars)
            if (chunkText.Length < options.MinChunkChars)
            {
                filteredCount++;
                logger.LogDebug("Filtered out chunk {Index} for document {DocumentId} (length {Length} < MinChunkChars {MinChars})", 
                    chunkIndex, sourceDocumentId, chunkText.Length, options.MinChunkChars);
                continue;
            }

            // SemanticSlicer does not produce embeddings - they will be generated later via queue
            yield return new TextChunk
            {
                Id = GenerateChunkId(sourceDocumentId, chunkIndex),
                Text = chunkText,
                Index = documentChunk.Index,
                SourceDocumentId = sourceDocumentId,
                Embedding = null // No embedding - will be queued for generation
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

