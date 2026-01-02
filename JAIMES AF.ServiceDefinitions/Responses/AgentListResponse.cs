namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public record AgentListResponse
{
    public required AgentResponse[] Agents { get; init; }
}



