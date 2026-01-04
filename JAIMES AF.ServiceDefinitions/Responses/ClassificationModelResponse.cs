namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response DTO for classification model information.
/// </summary>
public record ClassificationModelResponse(
    int Id,
    string ModelType,
    string Name,
    string? Description,
    string FileName,
    int StoredFileId,
    long? SizeBytes,
    DateTime CreatedAt,
    string Status,
    bool IsActive);
