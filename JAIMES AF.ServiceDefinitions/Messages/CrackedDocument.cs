using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class CrackedDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [BsonElement("relativeDirectory")]
    public string RelativeDirectory { get; set; } = string.Empty;

    [BsonElement("fileName")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("crackedAt")]
    public DateTime CrackedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("fileSize")]
    public long FileSize { get; set; }

    [BsonElement("pageCount")]
    public int PageCount { get; set; }
}

