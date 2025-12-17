namespace MattEland.Jaimes.ServiceDefinitions.Services;

public interface IScenariosService
{
    Task<ScenarioDto[]> GetScenariosAsync(CancellationToken cancellationToken = default);
    Task<ScenarioDto> GetScenarioAsync(string id, CancellationToken cancellationToken = default);

    Task<ScenarioDto> CreateScenarioAsync(string id,
        string rulesetId,
        string? description,
        string name,
        string systemPrompt,
        string? initialGreeting,
        CancellationToken cancellationToken = default);

    Task<ScenarioDto> UpdateScenarioAsync(string id,
        string rulesetId,
        string? description,
        string name,
        string systemPrompt,
        string? initialGreeting,
        CancellationToken cancellationToken = default);
    
    Task UpdateScenarioInstructionsAsync(string id, string? scenarioInstructions, CancellationToken cancellationToken = default);
}