using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for retrieving dashboard statistics for the home page.
/// </summary>
public class GetDashboardStatsEndpoint : EndpointWithoutRequest<DashboardStatsResponse>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/admin/dashboard/stats");
        AllowAnonymous();
        Description(b => b
            .Produces<DashboardStatsResponse>(StatusCodes.Status200OK)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Execute all count queries in parallel for optimal performance
        var gamesTask = DbContext.Games.CountAsync(ct);
        var scenariosTask = DbContext.Scenarios.CountAsync(ct);
        var playersTask = DbContext.Players.CountAsync(ct);
        var rulesetsTask = DbContext.Rulesets.CountAsync(ct);
        var agentsTask = DbContext.Agents.CountAsync(ct);
        var versionsTask = DbContext.AgentInstructionVersions.CountAsync(ct);
        var messagesTask = DbContext.Messages.CountAsync(ct);
        var mlModelsTask = DbContext.ClassificationModels.CountAsync(ct);
        var sentimentsTask = DbContext.MessageSentiments.CountAsync(ct);
        var feedbackTask = DbContext.MessageFeedbacks.CountAsync(ct);
        var evaluationsTask = DbContext.MessageEvaluationMetrics.CountAsync(ct);

        await Task.WhenAll(gamesTask, scenariosTask, playersTask, rulesetsTask,
            agentsTask, versionsTask, messagesTask, mlModelsTask,
            sentimentsTask, feedbackTask, evaluationsTask);

        await Send.OkAsync(new DashboardStatsResponse
        {
            GamesCount = gamesTask.Result,
            ScenariosCount = scenariosTask.Result,
            PlayersCount = playersTask.Result,
            RulesetsCount = rulesetsTask.Result,
            AgentsCount = agentsTask.Result,
            VersionsCount = versionsTask.Result,
            MessagesCount = messagesTask.Result,
            MlModelsCount = mlModelsTask.Result,
            SentimentsCount = sentimentsTask.Result,
            FeedbackCount = feedbackTask.Result,
            EvaluationsCount = evaluationsTask.Result
        }, ct);
    }
}
