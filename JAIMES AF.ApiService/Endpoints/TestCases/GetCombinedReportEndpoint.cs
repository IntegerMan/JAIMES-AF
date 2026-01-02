using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint for viewing/downloading a combined test report (multiple executions).
/// </summary>
public class GetCombinedReportEndpoint : EndpointWithoutRequest
{
    public required ITestCaseReportService ReportService { get; set; }

    public override void Configure()
    {
        Get("/test-case-runs/combined-report");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "text/html")
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? executionsParam = Query<string?>("executions", isRequired: false);

        if (string.IsNullOrEmpty(executionsParam))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var executionNames = executionsParam
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (executionNames.Count == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            string html = await ReportService.GenerateCombinedReportAsync(executionNames, ct);
            HttpContext.Response.ContentType = "text/html";
            await HttpContext.Response.WriteAsync(html, ct);
        }
        catch (ArgumentException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
