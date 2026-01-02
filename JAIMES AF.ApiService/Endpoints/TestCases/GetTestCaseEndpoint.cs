namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class GetTestCaseEndpoint : Ep.NoReq.Res<TestCaseResponse>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Get("/test-cases/{id:int}");
        AllowAnonymous();
        Description(b => b
            .Produces<TestCaseResponse>()
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");

        TestCaseResponse? testCase = await TestCaseService.GetTestCaseAsync(id, ct);
        if (testCase == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(testCase, ct);
    }
}
