namespace MattEland.Jaimes.ServiceDefinitions.Responses;

/// <summary>
/// Response containing all statistics for the home page dashboard.
/// </summary>
public record DashboardStatsResponse
{
    public int GamesCount { get; init; }
    public int ScenariosCount { get; init; }
    public int PlayersCount { get; init; }
    public int RulesetsCount { get; init; }
    public int AgentsCount { get; init; }
    public int VersionsCount { get; init; }
    public int MessagesCount { get; init; }
    public int MlModelsCount { get; init; }
    public int SentimentsCount { get; init; }
    public int FeedbackCount { get; init; }
    public int EvaluationsCount { get; init; }
}
