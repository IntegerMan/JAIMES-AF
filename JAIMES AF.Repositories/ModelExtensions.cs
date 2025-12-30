using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MattEland.Jaimes.Repositories;

/// <summary>
/// Extension methods for working with Model entities.
/// </summary>
public static class ModelExtensions
{
    private static readonly ConcurrentDictionary<string, int> ModelCache = new();

    /// <summary>
    /// Gets or creates a Model entity based on the provided model information.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="modelName">The model name (e.g., "gpt-4o-mini", "gemma3").</param>
    /// <param name="modelProvider">The provider type (e.g., "Ollama", "AzureOpenAI", "OpenAI").</param>
    /// <param name="modelEndpoint">The endpoint URL (optional).</param>
    /// <param name="logger">The logger (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created Model entity, or null if modelName is null/empty.</returns>
    public static async Task<Model?> GetOrCreateModelAsync(
        this JaimesDbContext context,
        string? modelName,
        string? modelProvider,
        string? modelEndpoint,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(modelProvider))
        {
            return null;
        }

        // Normalize name and provider
        string normalizedName = modelName.Trim();
        string normalizedProvider = modelProvider.Trim();

        // Normalize endpoint - treat null and empty string as the same, remove trailing slash, and lowercase
        string? normalizedEndpoint = string.IsNullOrWhiteSpace(modelEndpoint)
            ? null
            : modelEndpoint.Trim().TrimEnd('/').ToLowerInvariant();

        // Cache key
        string cacheKey = $"{normalizedName}|{normalizedProvider}|{normalizedEndpoint ?? ""}";

        // Check cache first
        if (ModelCache.TryGetValue(cacheKey, out int modelId))
        {
            // If the model exists in the cache, it's already in the database.
            // Many callers just need the ID, but some might need the entity.
            // Check if it's already tracked in the context
            Model? cachedModel = context.Models.Local.FirstOrDefault(m => m.Id == modelId);
            if (cachedModel != null)
            {
                return cachedModel;
            }

            // Otherwise, fetch it from the database (this is fast by ID)
            return await context.Models.FindAsync([modelId], cancellationToken);
        }

        // Try to find existing model in DB
        Model? existingModel = await context.Models
            .FirstOrDefaultAsync(
                m => m.Name == normalizedName
                     && m.Provider == normalizedProvider
                     && m.Endpoint == normalizedEndpoint,
                cancellationToken);

        if (existingModel != null)
        {
            ModelCache.TryAdd(cacheKey, existingModel.Id);
            return existingModel;
        }

        // Create new model
        Model newModel = new()
        {
            Name = normalizedName,
            Provider = normalizedProvider,
            Endpoint = normalizedEndpoint,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            context.Models.Add(newModel);
            await context.SaveChangesAsync(cancellationToken);

            logger?.LogInformation("Created new Model entity: {Name} (Provider: {Provider}, Endpoint: {Endpoint})",
                newModel.Name,
                newModel.Provider,
                newModel.Endpoint ?? "(none)");

            ModelCache.TryAdd(cacheKey, newModel.Id);

            return newModel;
        }
        catch (DbUpdateException)
        {
            // If we get a unique constraint violation, it means another request created the model simultaneously.
            // Clear the failed entity from the context's change tracker
            context.Entry(newModel).State = EntityState.Detached;

            // Try one more time to find the existing model
            existingModel = await context.Models
                .FirstOrDefaultAsync(
                    m => m.Name == normalizedName
                         && m.Provider == normalizedProvider
                         && m.Endpoint == normalizedEndpoint,
                    cancellationToken);

            if (existingModel != null)
            {
                ModelCache.TryAdd(cacheKey, existingModel.Id);
                return existingModel;
            }

            // If we still can't find it, rethrow the original exception
            throw;
        }
    }
}
