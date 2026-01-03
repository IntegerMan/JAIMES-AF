namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint to delete a stored test report file while preserving test run metrics.
/// </summary>
public class DeleteTestReportEndpoint : Ep.Req<DeleteTestReportRequest>.Res<EmptyResponse>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Delete("/test-reports/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces<EmptyResponse>(204)
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(DeleteTestReportRequest req, CancellationToken ct)
    {
        bool deleted = await TestCaseService.DeleteReportFileAsync(req.Id, ct);

        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

/// <summary>
/// Request to delete a test report.
/// </summary>
public class DeleteTestReportRequest
{
    public int Id { get; set; }
}
