using MattEland.Jaimes.Workers.DocumentEmbeddings.Models;

namespace MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

public interface ITextChunkingStrategy
{
    IEnumerable<TextChunk> ChunkText(string text, string sourceDocumentId);
}



