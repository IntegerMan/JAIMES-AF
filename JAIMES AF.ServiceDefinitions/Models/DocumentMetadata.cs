using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MattEland.Jaimes.ServiceDefinitions.Models;

public class DocumentMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("filePath")]
    [BsonRequired]
    public string FilePath { get; set; } = string.Empty;

    [BsonElement("hash")]
    [BsonRequired]
    public string Hash { get; set; } = string.Empty;

    [BsonElement("lastScanned")]
    [BsonRequired]
    public DateTime LastScanned { get; set; }
}




