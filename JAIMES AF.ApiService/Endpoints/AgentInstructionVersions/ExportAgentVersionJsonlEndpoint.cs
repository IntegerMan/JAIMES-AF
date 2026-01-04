using MattEland.Jaimes.ServiceDefinitions.Services;
using System.Text;
using System.Text.Json;

namespace MattEland.Jaimes.ApiService.Endpoints.AgentInstructionVersions;

/// <summary>
/// Endpoint for exporting agent version message data in JSONL format for Microsoft Foundry evaluation.
/// </summary>
public class ExportAgentVersionJsonlEndpoint : EndpointWithoutRequest
{
    public required IMessageService MessageService { get; set; }

    public override void Configure()
    {
        Get("/agents/{agentId}/versions/{versionId}/export-jsonl");
        AllowAnonymous();
        Description(b => b
            .Produces(200, contentType: "application/octet-stream")
            .Produces(404)
            .WithTags("Agent Instruction Versions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string agentId = Route<string>("agentId")!;
        int versionId = Route<int>("versionId");

        if (string.IsNullOrEmpty(agentId) || versionId <= 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var exportData = await MessageService.GetJsonlExportDataAsync(agentId, versionId, ct);
        var exportList = exportData.ToList();

        if (exportList.Count == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Generate JSONL content (one JSON object per line)
        var jsonlBuilder = new StringBuilder();
        foreach (var record in exportList)
        {
            string json = JsonSerializer.Serialize(record, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            });
            jsonlBuilder.AppendLine(json);
        }

        var bytes = Encoding.UTF8.GetBytes(jsonlBuilder.ToString());

        // Generate filename with timestamp
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string sanitizedAgentId = new string(agentId.Where(c => !char.IsControl(c) && c != '"' && c != '\\').ToArray());
        string fileName = $"{sanitizedAgentId}-v{versionId}-{timestamp}.jsonl";

        HttpContext.Response.ContentType = "application/octet-stream";
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        await HttpContext.Response.Body.WriteAsync(bytes, ct);
    }
}
