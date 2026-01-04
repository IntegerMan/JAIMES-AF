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
    /// Gets the binary content of a stored model file.
    /// </summary>
    /// <param name="modelId">The ID of the classification model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model file content as bytes, or null if not found.</returns>
    Task<byte[]?> GetModelContentAsync(int modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a new classification model to the database.
    /// </summary>
    /// <param name="modelType">The type of model.</param>
    /// <param name="name">Descriptive name for the model.</param>
    /// <param name="fileName">Original filename.</param>
    /// <param name="content">Binary model content.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created model response.</returns>
    Task<ClassificationModelResponse> UploadModelAsync(
        string modelType,
        string name,
        string fileName,
        byte[] content,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all classification models for admin display.
    /// </summary>
    Task<List<ClassificationModelResponse>> GetAllModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Constants for classification model types.
/// </summary>
public static class ClassificationModelTypes
{
    public const string SentimentClassification = "SentimentClassification";
}
