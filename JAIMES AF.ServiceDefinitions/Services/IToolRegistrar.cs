namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for registering and updating tools in the database.
/// </summary>
public interface IToolRegistrar
{
    /// <summary>
    /// Scans for tools and registers them in the database if they are not already present.
    /// Updates existing tool metadata if it has changed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RegisterToolsAsync(CancellationToken cancellationToken = default);
}
