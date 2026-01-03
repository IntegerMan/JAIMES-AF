namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class GetTestCaseByMessageEndpoint : Ep.NoReq.Res<TestCaseResponse>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Get("/messages/{messageId:int}/test-case");
        AllowAnonymous();
        Description(b => b
            .Produces<TestCaseResponse>()
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int messageId = Route<int>("messageId");

        TestCaseResponse? testCase = await TestCaseService.GetTestCaseByMessageIdAsync(messageId, ct);
        if (testCase == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(testCase, ct);
    }
}
