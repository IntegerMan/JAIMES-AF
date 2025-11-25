using MattEland.Jaimes.Workers.DocumentChunking.Models;

namespace MattEland.Jaimes.Workers.DocumentChunking.Services;

public interface ITextChunkingStrategy
{
    IEnumerable<TextChunk> ChunkText(string text, string sourceDocumentId);
}

