using System.Diagnostics;
using FastEndpoints;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Evaluators;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for testing evaluators against a sample AI response.
/// </summary>
public class TestEvaluatorEndpoint : Endpoint<TestEvaluatorRequest, TestEvaluatorResponse>
{
    public required IAgentInstructionVersionsService InstructionVersionsService { get; set; }
    public required IEnumerable<IEvaluator> Evaluators { get; set; }
    public required IChatClient ChatClient { get; set; }
    public required IDbContextFactory<JaimesDbContext> ContextFactory { get; set; }

    public override void Configure()
    {
        Post("/admin/evaluators/test");
        AllowAnonymous();
        Description(b => b
            .Produces<TestEvaluatorResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Evaluators"));
    }

    public override async Task HandleAsync(TestEvaluatorRequest req, CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<string> errors = [];

        // Get the instruction version for system prompt
        AgentInstructionVersionDto? version = await InstructionVersionsService.GetInstructionVersionAsync(
            req.InstructionVersionId, ct);

        if (version == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Use override if provided, otherwise use the version's instructions
        string systemPrompt = !string.IsNullOrWhiteSpace(req.SystemPromptOverride)
            ? req.SystemPromptOverride
            : version.Instructions;

        // Get ruleset information if provided
        string? rulesetName = null;
        if (!string.IsNullOrWhiteSpace(req.RulesetId))
        {
            await using JaimesDbContext context = await ContextFactory.CreateDbContextAsync(ct);
            var ruleset = await context.Rulesets
                .AsNoTracking()
                .Where(r => r.Id == req.RulesetId)
                .Select(r => new { r.Name })
                .FirstOrDefaultAsync(ct);

            rulesetName = ruleset?.Name;
        }

        // Filter evaluators if specific ones are requested
        List<IEvaluator> evaluatorList = Evaluators.ToList();
        IEnumerable<IEvaluator> activeEvaluators = evaluatorList;

        if (req.EvaluatorNames.Count > 0)
        {
            HashSet<string> evaluatorNamesSet = new(req.EvaluatorNames, StringComparer.OrdinalIgnoreCase);
            activeEvaluators = evaluatorList
                .Where(e => evaluatorNamesSet.Contains(e.GetType().Name))
                .ToList();

            if (!activeEvaluators.Any())
            {
                ThrowError($"No matching evaluators found. Available evaluators: {string.Join(", ", evaluatorList.Select(e => e.GetType().Name))}");
                return;
            }
        }

        Logger.LogInformation(
            "Testing evaluators: {EvaluatorNames}",
            string.Join(", ", activeEvaluators.Select(e => e.GetType().Name)));

        try
        {
            // Build conversation context
            List<ChatMessage> chatMessages =
            [
                new ChatMessage(ChatRole.System, systemPrompt)
            ];

            // Add conversation context
            foreach (TestEvaluatorMessage msg in req.ConversationContext)
            {
                ChatRole role = msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.User
                    : ChatRole.Assistant;
                chatMessages.Add(new ChatMessage(role, msg.Text));
            }

            // Create the response to evaluate
            ChatMessage assistantMessage = new(ChatRole.Assistant, req.AssistantResponse);
            ChatResponse assistantResponse = new(assistantMessage);

            // Create composite evaluator
            IEvaluator compositeEvaluator = new CompositeEvaluator(activeEvaluators);

            // Create chat configuration for evaluators that need it
            ChatConfiguration chatConfiguration = new(ChatClient);

            // Build additional context for evaluators that need it
            IEnumerable<EvaluationContext>? additionalContext = null;
            if (!string.IsNullOrWhiteSpace(req.RulesetId))
            {
                additionalContext = [new GameMechanicsEvaluationContext(req.RulesetId, rulesetName)];
            }

            // Run evaluation
            EvaluationResult result = await compositeEvaluator.EvaluateAsync(
                chatMessages,
                assistantResponse,
                chatConfiguration,
                additionalContext,
                ct);

            // Map evaluator names to their metrics
            Dictionary<string, string> metricToEvaluatorMap = new(StringComparer.OrdinalIgnoreCase);
            foreach (IEvaluator evaluator in activeEvaluators)
            {
                string className = evaluator.GetType().Name;
                foreach (string metricName in evaluator.EvaluationMetricNames)
                {
                    metricToEvaluatorMap[metricName] = className;
                }
            }

            // Build response
            List<TestEvaluatorMetricResult> metricResults = [];

            foreach (KeyValuePair<string, EvaluationMetric> metricPair in result.Metrics)
            {
                string metricName = metricPair.Key;
                EvaluationMetric metric = metricPair.Value;

                string evaluatorName = metricToEvaluatorMap.TryGetValue(metricName, out string? name)
                    ? name
                    : "Unknown";

                TestEvaluatorMetricResult metricResult = new()
                {
                    Name = metricName,
                    EvaluatorName = evaluatorName,
                    Score = metric is NumericMetric numericMetric ? numericMetric.Value : null,
                    Passed = metric.Interpretation?.Rating != EvaluationRating.Inconclusive
                             && metric.Interpretation?.Rating != EvaluationRating.Exceptional
                        ? metric.Interpretation?.Rating >= EvaluationRating.Good
                        : null,
                    Reason = metric.Reason,
                    Diagnostics = metric.Diagnostics?
                        .Select(d => new TestEvaluatorDiagnostic
                        {
                            Severity = d.Severity.ToString(),
                            Message = d.Message
                        })
                        .ToList() ?? []
                };

                metricResults.Add(metricResult);
            }

            stopwatch.Stop();

            TestEvaluatorResponse response = new()
            {
                Metrics = metricResults,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = errors,
                SystemPromptUsed = systemPrompt,
                RulesetNameUsed = rulesetName
            };

            await Send.OkAsync(response, ct);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error running evaluator test");
            stopwatch.Stop();

            errors.Add($"Evaluation failed: {ex.Message}");

            // Return a 200 with errors embedded in the response - the request was valid,
            // but the evaluation encountered an issue. This allows the UI to display the error.
            TestEvaluatorResponse errorResponse = new()
            {
                Metrics = [],
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = errors,
                SystemPromptUsed = systemPrompt,
                RulesetNameUsed = rulesetName
            };

            await Send.OkAsync(errorResponse, ct);
        }
    }
}
