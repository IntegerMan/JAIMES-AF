using FastEndpoints;
using Grpc.Core;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Workers.DocumentChunking.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class ListEmbeddingsEndpoint : Ep.NoReq.Res<EmbeddingListResponse>
{
    public required IQdrantEmbeddingStore EmbeddingStore { get; set; }

    public override void Configure()
    {
        Get("/embeddings");
        AllowAnonymous();
        Description(b => b
            .Produces<EmbeddingListResponse>()
            .WithTags("Embeddings")
            .WithSummary("List all embeddings with their metadata"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        Logger.LogInformation("Listing all embeddings");

        try
        {
            List<EmbeddingInfo> embeddings = await EmbeddingStore.ListEmbeddingsAsync(ct);

            EmbeddingListItem[] items = embeddings.Select(e => new EmbeddingListItem
            {
                EmbeddingId = e.PointId,
                DocumentId = e.DocumentId,
                FileName = e.FileName,
                ChunkId = e.ChunkId,
                PreviewText = e.ChunkText.Length > 140 ? e.ChunkText.Substring(0, 140) + "..." : e.ChunkText
            }).ToArray();

            Logger.LogInformation("Returning {Count} embeddings", items.Length);

            await Send.OkAsync(new EmbeddingListResponse
            {
                Embeddings = items
            }, cancellation: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant. Is Qdrant running and configured?");
            ThrowError("Unable to connect to Qdrant vector database. Please ensure Qdrant is running and properly configured.");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Logger.LogError(ex, "Failed to connect to Qdrant: {Message}", ex.Message);
            ThrowError($"Unable to connect to Qdrant vector database: {ex.Message}. Please ensure Qdrant is running and the connection settings are correct.");
        }
    }
}



