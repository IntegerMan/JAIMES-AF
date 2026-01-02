namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class DeleteTestCaseEndpoint : Ep.NoReq.NoRes
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Delete("/test-cases/{id:int}");
        AllowAnonymous();
        Description(b => b
            .Produces(204)
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");

        bool deleted = await TestCaseService.DeleteTestCaseAsync(id, ct);
        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Logger.LogInformation("Soft deleted test case {TestCaseId}", id);
        await Send.NoContentAsync(ct);
    }
}
