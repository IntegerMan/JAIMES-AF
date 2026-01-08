using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for getting a single evaluation metric's details.
/// </summary>
public class GetEvaluationEndpoint : EndpointWithoutRequest<EvaluationMetricListItemDto>
{
    public required JaimesDbContext DbContext { get; set; }

    public override void Configure()
    {
        Get("/admin/evaluations/{evaluationId}");
        AllowAnonymous();
        Description(b => b
            .Produces<EvaluationMetricListItemDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int evaluationId = Route<int>("evaluationId");

        var metric = await DbContext.MessageEvaluationMetrics
            .AsNoTracking()
            .Include(m => m.Message)
                .ThenInclude(msg => msg!.Game)
                    .ThenInclude(g => g!.Player)
            .Include(m => m.Message)
                .ThenInclude(msg => msg!.Game)
                    .ThenInclude(g => g!.Scenario)
            .Include(m => m.Message)
                .ThenInclude(msg => msg!.InstructionVersion)
                    .ThenInclude(iv => iv!.Agent)
            .Include(m => m.Evaluator)
            .Where(m => m.Id == evaluationId)
            .Select(m => new EvaluationMetricListItemDto
            {
                Id = m.Id,
                MessageId = m.MessageId,
                MetricName = m.MetricName,
                EvaluatorId = m.EvaluatorId,
                EvaluatorName = m.Evaluator != null ? m.Evaluator.Name : null,
                Score = m.Score,
                Passed = m.Score >= 3,
                Remarks = m.Remarks,
                Diagnostics = m.Diagnostics,
                EvaluatedAt = m.EvaluatedAt,
                GameId = m.Message!.GameId,
                GamePlayerName = m.Message.Game != null && m.Message.Game.Player != null
                    ? m.Message.Game.Player.Name
                    : null,
                GameScenarioName = m.Message.Game != null && m.Message.Game.Scenario != null
                    ? m.Message.Game.Scenario.Name
                    : null,
                GameRulesetId = m.Message.Game != null ? m.Message.Game.RulesetId : null,
                AgentId = m.Message.InstructionVersion != null ? m.Message.InstructionVersion.AgentId : null,
                AgentName = m.Message.InstructionVersion != null && m.Message.InstructionVersion.Agent != null
                    ? m.Message.InstructionVersion.Agent.Name
                    : null,
                InstructionVersionId = m.Message.InstructionVersionId,
                AgentVersion = m.Message.InstructionVersion != null
                    ? m.Message.InstructionVersion.VersionNumber
                    : null,
                MessagePreview = m.Message.Text != null && m.Message.Text.Length > 100
                    ? m.Message.Text.Substring(0, 100) + "..."
                    : m.Message.Text,
                IsMissing = false
            })
            .FirstOrDefaultAsync(ct);

        if (metric == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(metric, ct);
    }
}
