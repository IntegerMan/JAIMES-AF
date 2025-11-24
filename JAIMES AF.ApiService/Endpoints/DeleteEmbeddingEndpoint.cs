using FastEndpoints;
using Grpc.Core;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class DeleteEmbeddingEndpoint : Endpoint<DeleteEmbeddingRequest, DocumentOperationResponse>
{
    public required IQdrantEmbeddingStore EmbeddingStore { get; set; }

    public override void Configure()
    {
        Delete("/embeddings/{EmbeddingId}");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Embeddings")
            .WithSummary("Delete a specific embedding by ID"));
    }

    public override async Task HandleAsync(DeleteEmbeddingRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.EmbeddingId))
        {
            ThrowError("Embedding ID is required.");
            return;
        }

        Logger.LogInformation("Deleting embedding {EmbeddingId}", req.EmbeddingId);

        try
        {
            await EmbeddingStore.DeleteEmbeddingAsync(req.EmbeddingId, ct);

            Logger.LogInformation("Successfully deleted embedding {EmbeddingId}", req.EmbeddingId);

            await Send.OkAsync(new DocumentOperationResponse
            {
                Success = true,
                Message = $"Successfully deleted embedding {req.EmbeddingId}"
            }, cancellation: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant while deleting embedding {EmbeddingId}", req.EmbeddingId);
            ThrowError("Unable to connect to Qdrant vector database. Please ensure Qdrant is running and properly configured.");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant while deleting embedding {EmbeddingId}: {Message}", req.EmbeddingId, ex.Message);
            ThrowError($"Unable to connect to Qdrant vector database: {ex.Message}. Please ensure Qdrant is running and the connection settings are correct.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete embedding {EmbeddingId}", req.EmbeddingId);
            ThrowError($"Failed to delete embedding: {ex.Message}");
        }
    }
}



