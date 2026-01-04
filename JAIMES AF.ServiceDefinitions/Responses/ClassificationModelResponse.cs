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
    long? SizeBytes,
    DateTime CreatedAt);
