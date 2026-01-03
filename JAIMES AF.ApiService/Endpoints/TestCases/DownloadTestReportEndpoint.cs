using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint for downloading a test report as an HTML file.
/// </summary>
public class DownloadTestReportEndpoint : EndpointWithoutRequest
{
    public required ITestCaseReportService ReportService { get; set; }

    public override void Configure()
    {
        Get("/test-case-runs/{executionName}/report/download");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "application/octet-stream")
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

        var bytes = System.Text.Encoding.UTF8.GetBytes(report);
        HttpContext.Response.ContentType = "application/octet-stream";
        // Sanitize executionName to prevent header injection from special characters
        string sanitizedName =
            new string(executionName.Where(c => !char.IsControl(c) && c != '"' && c != '\\').ToArray());
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{sanitizedName}-report.html\"";
        await HttpContext.Response.Body.WriteAsync(bytes, ct);
    }
}
