namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class ListTestCasesEndpoint : Ep.NoReq.Res<List<TestCaseResponse>>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Get("/test-cases");
        AllowAnonymous();
        Description(b => b
            .Produces<List<TestCaseResponse>>()
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? agentId = Query<string?>("agentId", isRequired: false);
        bool includeInactive = Query<bool>("includeInactive", isRequired: false);

        List<TestCaseResponse> testCases = await TestCaseService.ListTestCasesAsync(agentId, includeInactive, ct);
        await Send.OkAsync(testCases, ct);
    }
}
