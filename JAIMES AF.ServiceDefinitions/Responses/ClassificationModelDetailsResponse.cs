namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Detailed response for a classification model including training metrics.
/// </summary>
public record ClassificationModelDetailsResponse(
    int Id,
    string ModelType,
    string Name,
    string? Description,
    string FileName,
    int StoredFileId,
    long? SizeBytes,
    DateTime CreatedAt,
    bool IsActive,
    TrainingJobDetailsDto? TrainingJob);

/// <summary>
/// Training job details embedded in model details response.
/// </summary>
public record TrainingJobDetailsDto(
    int Id,
    string Status,
    double MinConfidence,
    double TrainTestSplit,
    int TrainingTimeSeconds,
    string OptimizingMetric,
    int? TotalRows,
    int? TrainingRows,
    int? TestRows,
    double? MacroAccuracy,
    double? MicroAccuracy,
    double? MacroPrecision,
    double? MacroRecall,
    double? LogLoss,
    int[][]? ConfusionMatrix,
    string? TrainerName,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);
