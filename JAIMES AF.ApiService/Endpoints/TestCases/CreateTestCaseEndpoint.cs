namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class CreateTestCaseEndpoint : Ep.Req<CreateTestCaseRequest>.Res<TestCaseResponse>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Post("/test-cases");
        AllowAnonymous();
        Description(b => b
            .Produces<TestCaseResponse>(201)
            .Produces(400)
            .Produces(409)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CreateTestCaseRequest req, CancellationToken ct)
    {
        try
        {
            TestCaseResponse testCase = await TestCaseService.CreateTestCaseAsync(
                req.MessageId,
                req.Name,
                req.Description,
                ct);

            Logger.LogInformation("Created test case {TestCaseId} for message {MessageId}", testCase.Id, req.MessageId);
            await Send.CreatedAtAsync<GetTestCaseEndpoint>(new { id = testCase.Id }, testCase, cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Failed to create test case for message {MessageId}: {Message}", req.MessageId,
                ex.Message);
            ThrowError(ex.Message);
        }
    }
}
