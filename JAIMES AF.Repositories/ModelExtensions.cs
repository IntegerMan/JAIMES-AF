using MattEland.Jaimes.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.Repositories;

/// <summary>
/// Extension methods for working with Model entities.
/// </summary>
public static class ModelExtensions
{
    /// <summary>
    /// Gets or creates a Model entity based on the provided model information.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="modelName">The model name (e.g., "gpt-4o-mini", "gemma3").</param>
    /// <param name="modelProvider">The provider type (e.g., "Ollama", "AzureOpenAI", "OpenAI").</param>
    /// <param name="modelEndpoint">The endpoint URL (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created Model entity, or null if modelName is null/empty.</returns>
    public static async Task<Model?> GetOrCreateModelAsync(
        this JaimesDbContext context,
        string? modelName,
        string? modelProvider,
        string? modelEndpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName) || string.IsNullOrWhiteSpace(modelProvider))
        {
            return null;
        }

        // Normalize endpoint - treat null and empty string as the same
        string? normalizedEndpoint = string.IsNullOrWhiteSpace(modelEndpoint)
            ? null
            : modelEndpoint;

        // Try to find existing model
        Model? existingModel = await context.Models
            .FirstOrDefaultAsync(
                m => m.Name == modelName
                     && m.Provider == modelProvider
                     && m.Endpoint == normalizedEndpoint,
                cancellationToken);

        if (existingModel != null)
        {
            return existingModel;
        }

        // Create new model
        Model newModel = new()
        {
            Name = modelName,
            Provider = modelProvider,
            Endpoint = normalizedEndpoint,
            CreatedAt = DateTime.UtcNow
        };

        context.Models.Add(newModel);
        await context.SaveChangesAsync(cancellationToken);

        return newModel;
    }
}
