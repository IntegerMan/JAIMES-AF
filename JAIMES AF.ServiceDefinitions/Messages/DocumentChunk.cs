using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class DocumentChunk
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("chunkId")]
    public string ChunkId { get; set; } = string.Empty;

    [BsonElement("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [BsonElement("chunkText")]
    public string ChunkText { get; set; } = string.Empty;

    [BsonElement("chunkIndex")]
    public int ChunkIndex { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}



