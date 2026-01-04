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

        ClassificationModel? model = await context.ClassificationModels
            .Include(cm => cm.StoredFile)
            .Where(cm => cm.ModelType == modelType)
            .OrderByDescending(cm => cm.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (model == null)
        {
            return null;
        }

        return MapToResponse(model);
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
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

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
        ClassificationModel model = new()
        {
            ModelType = modelType,
            Name = name,
            Description = description,
            StoredFile = storedFile, // Use navigation property for atomicity
            CreatedAt = DateTime.UtcNow
        };

        context.ClassificationModels.Add(model);
        await context.SaveChangesAsync(cancellationToken); // Single save handles both entities in one transaction

        logger.LogInformation(
            "Uploaded classification model '{Name}' (Type: {ModelType}, Size: {Size} bytes)",
            name,
            modelType,
            content.Length);

        model.StoredFile = storedFile;
        return MapToResponse(model);
    }

    /// <inheritdoc />
    public async Task<List<ClassificationModelResponse>> GetAllModelsAsync(
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<ClassificationModel> models = await context.ClassificationModels
            .Include(cm => cm.StoredFile)
            .OrderByDescending(cm => cm.CreatedAt)
            .ToListAsync(cancellationToken);

        return models.Select(MapToResponse).ToList();
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
            model.CreatedAt);
    }
}
