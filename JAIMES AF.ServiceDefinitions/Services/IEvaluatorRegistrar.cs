namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service for registering and updating evaluators in the database.
/// </summary>
public interface IEvaluatorRegistrar
{
    /// <summary>
    /// Scans for evaluators and registers their metrics in the database if they are not already present.
    /// Updates existing evaluator metadata if it has changed.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RegisterEvaluatorsAsync(CancellationToken cancellationToken = default);
}
