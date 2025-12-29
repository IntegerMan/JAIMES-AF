using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.Agents;

public class DeleteAgentEndpoint : EndpointWithoutRequest
{
    public required IAgentsService AgentsService { get; set; }

    public override void Configure()
    {
        Delete("/agents/{id}");
        AllowAnonymous();
        Description(b => b
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        string? id = Route<string>("id", true);
        if (string.IsNullOrEmpty(id))
        {
            ThrowError("Agent ID is required");
            return;
        }

        try
        {
            await AgentsService.DeleteAgentAsync(id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

