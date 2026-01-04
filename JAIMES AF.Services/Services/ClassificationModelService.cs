using System.Text.Json;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for managing classification models stored in the database.
/// </summary>
public class ClassificationModelService(
    IDbContextFactory<JaimesDbContext> contextFactory,
    ILogger<ClassificationModelService> logger) : IClassificationModelService
{
    private const string ModelItemKind = "ClassificationModel";

    /// <inheritdoc />
    public async Task<ClassificationModelResponse?> GetLatestModelAsync(
        string modelType,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ClassificationModels
            .Where(cm => cm.ModelType == modelType)
            .OrderByDescending(cm => cm.CreatedAt)
            .Select(cm => new ClassificationModelResponse(
                cm.Id,
                cm.ModelType,
                cm.Name,
                cm.Description,
                cm.StoredFile.FileName,
                cm.StoredFileId,
                cm.StoredFile.SizeBytes,
                cm.CreatedAt,
                "Ready",
                cm.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClassificationModelResponse?> GetActiveModelAsync(
        string modelType,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ClassificationModels
            .Where(cm => cm.ModelType == modelType && cm.IsActive)
            .Select(cm => new ClassificationModelResponse(
                cm.Id,
                cm.ModelType,
                cm.Name,
                cm.Description,
                cm.StoredFile.FileName,
                cm.StoredFileId,
                cm.StoredFile.SizeBytes,
                cm.CreatedAt,
                "Ready",
                cm.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetModelContentAsync(
        int modelId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ClassificationModels
            .Where(cm => cm.Id == modelId)
            .Select(cm => cm.StoredFile.BinaryContent)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClassificationModelResponse> UploadModelAsync(
        string modelType,
        string name,
        string fileName,
        byte[] content,
        string? description = null,
        int? trainingJobId = null,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Check if there's already an active model of this type
        bool hasActiveModel = await context.ClassificationModels
            .AnyAsync(cm => cm.ModelType == modelType && cm.IsActive, cancellationToken);

        // Create the stored file for binary content
        StoredFile storedFile = new()
        {
            ItemKind = ModelItemKind,
            FileName = fileName,
            ContentType = "application/zip",
            BinaryContent = content,
            CreatedAt = DateTime.UtcNow,
            SizeBytes = content.Length
        };

        context.StoredFiles.Add(storedFile);

        // Create the classification model record referencing the navigation property
        // Auto-activate if no active model exists for this type
        ClassificationModel model = new()
        {
            ModelType = modelType,
            Name = name,
            Description = description,
            StoredFile = storedFile,
            CreatedAt = DateTime.UtcNow,
            TrainingJobId = trainingJobId,
            IsActive = !hasActiveModel // Auto-activate first model of a type
        };

        context.ClassificationModels.Add(model);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Uploaded classification model '{Name}' (Type: {ModelType}, Size: {Size} bytes, Active: {IsActive})",
            name,
            modelType,
            content.Length,
            model.IsActive);

        model.StoredFile = storedFile;
        return MapToResponse(model);
    }

    /// <inheritdoc />
    public async Task<List<ClassificationModelResponse>> GetAllModelsAsync(
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ClassificationModels
            .OrderByDescending(cm => cm.CreatedAt)
            .Select(cm => new ClassificationModelResponse(
                cm.Id,
                cm.ModelType,
                cm.Name,
                cm.Description,
                cm.StoredFile.FileName,
                cm.StoredFileId,
                cm.StoredFile.SizeBytes,
                cm.CreatedAt,
                "Ready",
                cm.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<ClassificationModelResponse>> GetAllModelsWithTrainingJobsAsync(
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Get completed models
        List<ClassificationModelResponse> models = await context.ClassificationModels
            .OrderByDescending(cm => cm.CreatedAt)
            .Select(cm => new ClassificationModelResponse(
                cm.Id,
                cm.ModelType,
                cm.Name,
                cm.Description,
                cm.StoredFile.FileName,
                cm.StoredFileId,
                cm.StoredFile.SizeBytes,
                cm.CreatedAt,
                "Ready",
                cm.IsActive))
            .ToListAsync(cancellationToken);

        // Get pending and failed training jobs (not completed successfully - those become models)
        List<ClassificationModelResponse> pendingJobs = await context.ClassificationModelTrainingJobs
            .Where(tj => tj.Status == "Queued" || tj.Status == "Training" || tj.Status == "Failed")
            .OrderByDescending(tj => tj.CreatedAt)
            .Select(tj => new ClassificationModelResponse(
                -tj.Id, // Use negative ID to distinguish from real models
                ClassificationModelTypes.SentimentClassification,
                tj.Status == "Failed" ? $"Failed Training Job #{tj.Id}" : $"Training Job #{tj.Id}",
                tj.Status == "Failed"
                    ? (tj.ErrorMessage ?? "Training failed")
                    : $"Parameters: {tj.MinConfidence:P0} confidence, {tj.TrainTestSplit:P0} split, {tj.TrainingTimeSeconds}s",
                "",
                0,
                null,
                tj.CreatedAt,
                tj.Status == "Training" ? "Training..." : tj.Status,
                false))
            .ToListAsync(cancellationToken);

        // Combine and sort by date
        return models.Concat(pendingJobs)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ClassificationModelDetailsResponse?> GetModelDetailsAsync(
        int modelId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ClassificationModel? model = await context.ClassificationModels
            .Include(cm => cm.StoredFile)
            .Include(cm => cm.TrainingJob)
            .FirstOrDefaultAsync(cm => cm.Id == modelId, cancellationToken);

        if (model == null)
        {
            return null;
        }

        TrainingJobDetailsDto? trainingJobDto = null;
        if (model.TrainingJob != null)
        {
            int[][]? confusionMatrix = null;
            if (!string.IsNullOrEmpty(model.TrainingJob.ConfusionMatrixJson))
            {
                try
                {
                    confusionMatrix = JsonSerializer.Deserialize<int[][]>(model.TrainingJob.ConfusionMatrixJson);
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            trainingJobDto = new TrainingJobDetailsDto(
                model.TrainingJob.Id,
                model.TrainingJob.Status,
                model.TrainingJob.MinConfidence,
                model.TrainingJob.TrainTestSplit,
                model.TrainingJob.TrainingTimeSeconds,
                model.TrainingJob.OptimizingMetric,
                model.TrainingJob.TotalRows,
                model.TrainingJob.TrainingRows,
                model.TrainingJob.TestRows,
                model.TrainingJob.MacroAccuracy,
                model.TrainingJob.MicroAccuracy,
                model.TrainingJob.MacroPrecision,
                model.TrainingJob.MacroRecall,
                model.TrainingJob.LogLoss,
                confusionMatrix,
                model.TrainingJob.TrainerName,
                model.TrainingJob.CreatedAt,
                model.TrainingJob.CompletedAt,
                model.TrainingJob.ErrorMessage);
        }

        return new ClassificationModelDetailsResponse(
            model.Id,
            model.ModelType,
            model.Name,
            model.Description,
            model.StoredFile.FileName,
            model.StoredFileId,
            model.StoredFile.SizeBytes,
            model.CreatedAt,
            model.IsActive,
            trainingJobDto);
    }

    /// <inheritdoc />
    public async Task<bool> ActivateModelAsync(int modelId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ClassificationModel? model = await context.ClassificationModels
            .FirstOrDefaultAsync(cm => cm.Id == modelId, cancellationToken);

        if (model == null)
        {
            return false;
        }

        // Deactivate all models of the same type
        await context.ClassificationModels
            .Where(cm => cm.ModelType == model.ModelType && cm.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(cm => cm.IsActive, false), cancellationToken);

        // Activate the selected model
        model.IsActive = true;
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Activated classification model '{Name}' (ID: {ModelId}, Type: {ModelType})",
            model.Name,
            model.Id,
            model.ModelType);

        return true;
    }

    /// <inheritdoc />
    public async Task<TrainingDataCountResponse> GetTrainingDataCountAsync(
        double minConfidence,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        int totalWithSentiment = await context.MessageSentiments
            .Where(ms => ms.SentimentSource == SentimentSource.Model || ms.SentimentSource == SentimentSource.Player)
            .CountAsync(cancellationToken);

        int aboveConfidence = await context.MessageSentiments
            .Where(ms =>
                (ms.SentimentSource == SentimentSource.Model || ms.SentimentSource == SentimentSource.Player) &&
                (ms.SentimentSource == SentimentSource.Player ||
                 (ms.Confidence != null && ms.Confidence >= minConfidence)))
            .CountAsync(cancellationToken);

        return new TrainingDataCountResponse(totalWithSentiment, aboveConfidence, minConfidence);
    }

    /// <inheritdoc />
    public async Task<int> CreateTrainingJobAsync(
        double minConfidence,
        double trainTestSplit,
        int trainingTimeSeconds,
        string optimizingMetric,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ClassificationModelTrainingJob job = new()
        {
            Status = "Queued",
            MinConfidence = minConfidence,
            TrainTestSplit = trainTestSplit,
            TrainingTimeSeconds = trainingTimeSeconds,
            OptimizingMetric = optimizingMetric,
            CreatedAt = DateTime.UtcNow
        };

        context.ClassificationModelTrainingJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created training job #{JobId} (Confidence: {Confidence:P0}, Split: {Split:P0}, Time: {Time}s, Metric: {Metric})",
            job.Id,
            minConfidence,
            trainTestSplit,
            trainingTimeSeconds,
            optimizingMetric);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task UpdateTrainingJobAsync(
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
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        ClassificationModelTrainingJob? job = await context.ClassificationModelTrainingJobs
            .FirstOrDefaultAsync(tj => tj.Id == jobId, cancellationToken);

        if (job == null)
        {
            logger.LogWarning("Training job #{JobId} not found for update", jobId);
            return;
        }

        job.Status = status;
        if (totalRows.HasValue) job.TotalRows = totalRows;
        if (trainingRows.HasValue) job.TrainingRows = trainingRows;
        if (testRows.HasValue) job.TestRows = testRows;
        if (macroAccuracy.HasValue) job.MacroAccuracy = macroAccuracy;
        if (microAccuracy.HasValue) job.MicroAccuracy = microAccuracy;
        if (macroPrecision.HasValue) job.MacroPrecision = macroPrecision;
        if (macroRecall.HasValue) job.MacroRecall = macroRecall;
        if (logLoss.HasValue) job.LogLoss = logLoss;
        if (confusionMatrix != null) job.ConfusionMatrixJson = JsonSerializer.Serialize(confusionMatrix);
        if (trainerName != null) job.TrainerName = trainerName;
        if (classificationModelId.HasValue) job.ClassificationModelId = classificationModelId;
        if (errorMessage != null) job.ErrorMessage = errorMessage;

        if (status is "Completed" or "Failed")
        {
            job.CompletedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Updated training job #{JobId} to status '{Status}'",
            jobId,
            status);
    }

    private static ClassificationModelResponse MapToResponse(ClassificationModel model)
    {
        return new ClassificationModelResponse(
            model.Id,
            model.ModelType,
            model.Name,
            model.Description,
            model.StoredFile.FileName,
            model.StoredFileId,
            model.StoredFile.SizeBytes,
            model.CreatedAt,
            "Ready",
            model.IsActive);
    }
}

