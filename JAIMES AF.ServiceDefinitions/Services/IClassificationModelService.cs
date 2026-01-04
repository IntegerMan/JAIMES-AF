using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for managing classification models stored in the database.
/// </summary>
public interface IClassificationModelService
{
    /// <summary>
    /// Gets the latest classification model of the specified type.
    /// </summary>
    /// <param name="modelType">The type of model (e.g., "SentimentClassification").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest model info, or null if none exists.</returns>
    Task<ClassificationModelResponse?> GetLatestModelAsync(string modelType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active classification model of the specified type.
    /// </summary>
    Task<ClassificationModelResponse?> GetActiveModelAsync(string modelType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the binary content of a stored model file.
    /// </summary>
    /// <param name="modelId">The ID of the classification model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model file content as bytes, or null if not found.</returns>
    Task<byte[]?> GetModelContentAsync(int modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a new classification model to the database.
    /// </summary>
    Task<ClassificationModelResponse> UploadModelAsync(
        string modelType,
        string name,
        string fileName,
        byte[] content,
        string? description = null,
        int? trainingJobId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all classification models for admin display.
    /// </summary>
    Task<List<ClassificationModelResponse>> GetAllModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all models including pending training jobs.
    /// </summary>
    Task<List<ClassificationModelResponse>> GetAllModelsWithTrainingJobsAsync(CancellationToken cancellationToken =
        default);

    /// <summary>
    /// Gets detailed information about a specific model including training metrics.
    /// </summary>
    Task<ClassificationModelDetailsResponse?> GetModelDetailsAsync(int modelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a model as the active model for its type.
    /// </summary>
    Task<bool> ActivateModelAsync(int modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of messages available for training based on confidence threshold.
    /// </summary>
    Task<TrainingDataCountResponse> GetTrainingDataCountAsync(double minConfidence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new training job.
    /// </summary>
    Task<int> CreateTrainingJobAsync(
        double minConfidence,
        double trainTestSplit,
        int trainingTimeSeconds,
        string optimizingMetric,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a training job's status and metrics after training completes.
    /// </summary>
    Task UpdateTrainingJobAsync(
        int jobId,
        string status,
        int? totalRows = null,
        int? trainingRows = null,
        int? testRows = null,
        double? macroAccuracy = null,
        double? microAccuracy = null,
        double? macroPrecision = null,
        double? macroRecall = null,
        double? logLoss = null,
        int[][]? confusionMatrix = null,
        string? trainerName = null,
        int? classificationModelId = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Constants for classification model types.
/// </summary>
public static class ClassificationModelTypes
{
    public const string SentimentClassification = "SentimentClassification";
}

