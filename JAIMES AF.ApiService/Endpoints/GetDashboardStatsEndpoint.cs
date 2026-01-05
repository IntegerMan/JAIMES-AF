using MattEland.Jaimes.Domain;
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
        // Execute queries sequentially. DbContext is not thread-safe for parallel operations.
        int gamesCount = await DbContext.Games.CountAsync(ct);
        int scenariosCount = await DbContext.Scenarios.CountAsync(ct);
        int playersCount = await DbContext.Players.CountAsync(ct);
        int rulesetsCount = await DbContext.Rulesets.CountAsync(ct);
        int agentsCount = await DbContext.Agents.CountAsync(ct);
        int versionsCount = await DbContext.AgentInstructionVersions.CountAsync(ct);
        int messagesCount = await DbContext.Messages.CountAsync(ct);
        int mlModelsCount = await DbContext.ClassificationModels.CountAsync(ct);
        int sentimentsCount = await DbContext.MessageSentiments.CountAsync(ct);
        int feedbackCount = await DbContext.MessageFeedbacks.CountAsync(ct);
        int evaluationsCount = await DbContext.MessageEvaluationMetrics.CountAsync(ct);
        int testCasesCount = await DbContext.TestCases.CountAsync(ct);
        int testReportsCount = await DbContext.StoredFiles.CountAsync(f => f.ItemKind == "TestReport", ct);
        int sourcebooksCount =
            await DbContext.CrackedDocuments.CountAsync(d => d.DocumentKind == DocumentKinds.Sourcebook, ct);
        int locationsCount = await DbContext.Locations.CountAsync(ct);

        await Send.OkAsync(new DashboardStatsResponse
        {
            GamesCount = gamesCount,
            ScenariosCount = scenariosCount,
            PlayersCount = playersCount,
            RulesetsCount = rulesetsCount,
            AgentsCount = agentsCount,
            VersionsCount = versionsCount,
            MessagesCount = messagesCount,
            MlModelsCount = mlModelsCount,
            SentimentsCount = sentimentsCount,
            FeedbackCount = feedbackCount,
            EvaluationsCount = evaluationsCount,
            TestCasesCount = testCasesCount,
            TestReportsCount = testReportsCount,
            SourcebooksCount = sourcebooksCount,
            LocationsCount = locationsCount
        }, ct);
    }
}
