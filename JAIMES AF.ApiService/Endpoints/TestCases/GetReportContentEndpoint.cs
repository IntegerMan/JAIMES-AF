namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

/// <summary>
/// Endpoint to get a stored report's content by ID.
/// </summary>
public class GetReportContentEndpoint : EndpointWithoutRequest
{
    public required ITestCaseReportService ReportService { get; set; }

    public override void Configure()
    {
        Get("/test-reports/{id}/content");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "text/html")
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int id = Route<int>("id");
        
        string? content = await ReportService.GetReportContentByIdAsync(id, ct);
        
        if (content == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        HttpContext.Response.ContentType = "text/html";
        await HttpContext.Response.WriteAsync(content, ct);
    }
}
