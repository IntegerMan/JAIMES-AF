namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IInstructionService
{
    Task<string?> GetInstructionsAsync(string scenarioId, CancellationToken cancellationToken = default);
}

