using MattEland.Jaimes.ServiceDefinitions.Models;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface ITextChunkingStrategy
{
    IEnumerable<TextChunk> ChunkText(string text, string sourceDocumentId);
}
