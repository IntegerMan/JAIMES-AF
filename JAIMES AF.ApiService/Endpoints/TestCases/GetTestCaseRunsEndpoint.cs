namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class GetTestCaseRunsEndpoint : Ep.NoReq.Res<List<TestCaseRunResponse>>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Get("/test-case-runs");
        AllowAnonymous();
        Description(b => b
            .Produces<List<TestCaseRunResponse>>()
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? executionName = Query<string?>("executionName", isRequired: false);
        int? testCaseId = Query<int?>("testCaseId", isRequired: false);

        List<TestCaseRunResponse> runs;

        if (!string.IsNullOrEmpty(executionName))
        {
            runs = await TestCaseService.GetRunsByExecutionAsync(executionName, ct);
        }
        else if (testCaseId.HasValue)
        {
            runs = await TestCaseService.GetRunsByTestCaseAsync(testCaseId.Value, ct);
        }
        else
        {
            // Return all runs if no filter provided
            runs = await TestCaseService.GetAllRunsAsync(ct: ct);
        }

        await Send.OkAsync(runs, ct);
    }
}
