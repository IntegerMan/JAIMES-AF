namespace MattEland.Jaimes.ServiceDefinitions.Requests;

/// <summary>
/// Request to test evaluators against a sample AI response.
/// </summary>
public record TestEvaluatorRequest
{
    /// <summary>
    /// The ID of the agent instruction version to use for the system prompt.
    /// </summary>
    public int InstructionVersionId { get; init; }

    /// <summary>
    /// Optional override for the system prompt. If provided, this will be used instead of the instruction version's instructions.
    /// </summary>
    public string? SystemPromptOverride { get; init; }

    /// <summary>
    /// The ID of the ruleset to use for game mechanics evaluation.
    /// Required for GameMechanicsEvaluator, optional for other evaluators.
    /// </summary>
    public string? RulesetId { get; init; }

    /// <summary>
    /// The names of evaluators to run. If empty, all evaluators will be run.
    /// </summary>
    public List<string> EvaluatorNames { get; init; } = [];

    /// <summary>
    /// The conversation context (prior messages) leading up to the response being evaluated.
    /// </summary>
    public List<TestEvaluatorMessage> ConversationContext { get; init; } = [];

    /// <summary>
    /// The AI assistant response to evaluate.
    /// </summary>
    public required string AssistantResponse { get; init; }
}

/// <summary>
/// Represents a message in the conversation context for evaluator testing.
/// </summary>
public record TestEvaluatorMessage
{
    /// <summary>
    /// The role of the message sender. Should be "user" or "assistant".
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public required string Text { get; init; }
}
