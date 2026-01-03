using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint for running tests against multiple agent versions in a single unified report.
/// </summary>
public class RunMultiVersionTestsEndpoint : Ep.Req<RunMultiVersionTestsRequest>.Res<MultiVersionTestRunResponse>
{
    public required IAgentTestRunner AgentTestRunner { get; set; }

    public override void Configure()
    {
        Post("/test-runs/multi-version");
        AllowAnonymous();
        Description(b => b
            .Produces<MultiVersionTestRunResponse>()
            .Produces(400)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(RunMultiVersionTestsRequest req, CancellationToken ct)
    {
        if (req.Versions.Count == 0)
        {
            ThrowError("At least one version is required");
            return;
        }

        Logger.LogInformation(
            "Starting multi-version test run for {VersionCount} versions",
            req.Versions.Count);

        try
        {
            var versions = req.Versions.Select(v => new VersionToTest
            {
                AgentId = v.AgentId,
                InstructionVersionId = v.InstructionVersionId
            });

            MultiVersionTestRunResponse result = await AgentTestRunner.RunMultiVersionTestsAsync(
                versions,
                req.TestCaseIds,
                req.ExecutionName,
                req.EvaluatorNames,
                ct);

            Logger.LogInformation(
                "Multi-version test run {ExecutionName} completed: {Completed}/{Total} runs",
                result.ExecutionName, result.CompletedRuns, result.TotalRuns);

            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid request: {Message}", ex.Message);
            ThrowError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Test run failed: {Message}", ex.Message);
            ThrowError(ex.Message);
        }
    }
}
