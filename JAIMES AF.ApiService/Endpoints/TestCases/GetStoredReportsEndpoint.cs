namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint to get stored test reports.
/// </summary>
public class GetStoredReportsEndpoint : Ep.NoReq.Res<List<StoredReportResponse>>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Get("/test-reports");
        AllowAnonymous();
        Description(b => b
            .Produces<List<StoredReportResponse>>()
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var reports = await TestCaseService.GetStoredReportsAsync(ct: ct);
        await Send.OkAsync(reports, ct);
    }
}
