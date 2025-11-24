using FastEndpoints;
using Grpc.Core;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Workers.DocumentEmbeddings.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class DeleteAllEmbeddingsEndpoint : Ep.NoReq.Res<DocumentOperationResponse>
{
    public required IQdrantEmbeddingStore EmbeddingStore { get; set; }

    public override void Configure()
    {
        Delete("/embeddings");
        AllowAnonymous();
        Description(b => b
            .Produces<DocumentOperationResponse>()
            .WithTags("Embeddings")
            .WithSummary("Delete all embeddings from the collection"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Deleting all embeddings");

        try
        {
            await EmbeddingStore.DeleteAllEmbeddingsAsync(ct);

            Logger.LogInformation("Successfully deleted all embeddings");

            await Send.OkAsync(new DocumentOperationResponse
            {
                Success = true,
                Message = "Successfully deleted all embeddings"
            }, cancellation: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant while deleting all embeddings");
            ThrowError("Unable to connect to Qdrant vector database. Please ensure Qdrant is running and properly configured.");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant while deleting all embeddings: {Message}", ex.Message);
            ThrowError($"Unable to connect to Qdrant vector database: {ex.Message}. Please ensure Qdrant is running and the connection settings are correct.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete all embeddings");
            ThrowError($"Failed to delete all embeddings: {ex.Message}");
        }
    }
}



