namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentListResponse
{
    public required AgentResponse[] Agents { get; init; }
    public int? TotalAgents { get; init; }
    public int? TotalVersions { get; init; }
    public int? TotalFeedback { get; init; }
    public double? AverageEvaluation { get; init; }
}



