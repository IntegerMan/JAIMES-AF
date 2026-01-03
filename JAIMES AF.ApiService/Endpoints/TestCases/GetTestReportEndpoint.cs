using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint for viewing a test report as HTML (for iframe display).
/// </summary>
public class GetTestReportEndpoint : EndpointWithoutRequest
{
    public required ITestCaseReportService ReportService { get; set; }

    public override void Configure()
    {
        Get("/test-case-runs/{executionName}/report");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "text/html")
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string executionName = Route<string>("executionName")!;

        string? report = await ReportService.GetStoredReportAsync(executionName, ct);

        if (report == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        HttpContext.Response.ContentType = "text/html";
        await HttpContext.Response.WriteAsync(report, ct);
    }
}
