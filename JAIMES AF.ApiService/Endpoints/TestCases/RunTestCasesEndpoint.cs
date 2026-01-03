namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class RunTestCasesEndpoint : Ep.Req<RunTestCasesRequest>.Res<TestRunResultResponse>
{
    public required IAgentTestRunner AgentTestRunner { get; set; }

    public override void Configure()
    {
        Post("/agents/{agentId}/versions/{versionId:int}/test-run");
        AllowAnonymous();
        Description(b => b
            .Produces<TestRunResultResponse>()
            .Produces(400)
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(RunTestCasesRequest req, CancellationToken ct)
    {
        string agentId = Route<string>("agentId") ?? throw new InvalidOperationException("Agent ID is required");
        int versionId = Route<int>("versionId");

        try
        {
            Logger.LogInformation(
                "Starting test run for agent {AgentId} version {VersionId} with {TestCount} test cases",
                agentId, versionId, req.TestCaseIds?.Count ?? 0);

            TestRunResultResponse result = await AgentTestRunner.RunTestCasesAsync(
                agentId,
                versionId,
                req.TestCaseIds,
                req.ExecutionName,
                req.EvaluatorNames,
                ct);

            Logger.LogInformation("Test run {ExecutionName} completed: {Completed}/{Total} test cases",
                result.ExecutionName, result.CompletedTestCases, result.TotalTestCases);

            await Send.OkAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Test run failed: {Message}", ex.Message);
            ThrowError(ex.Message);
        }
    }
}
