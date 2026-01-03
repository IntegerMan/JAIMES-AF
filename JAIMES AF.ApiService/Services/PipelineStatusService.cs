using MattEland.Jaimes.ApiService.Hubs;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace MattEland.Jaimes.ApiService.Services;

/// <summary>
/// Service for tracking and broadcasting document processing pipeline status.
/// </summary>
public class PipelineStatusService : IPipelineStatusService
{
    private readonly IHubContext<PipelineStatusHub, IPipelineStatusHubClient> _hubContext;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IDbContextFactory<JaimesDbContext> _dbContextFactory;
    private readonly ILogger<PipelineStatusService> _logger;

    // Cache the last reported queue sizes from workers
    private readonly ConcurrentDictionary<string, int> _queueSizes = new();
    private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

    // Queue names based on message types
    private const string CrackingQueueName = nameof(CrackDocumentMessage);
    private const string ChunkingQueueName = nameof(DocumentReadyForChunkingMessage);
    private const string EmbeddingQueueName = nameof(ChunkReadyForEmbeddingMessage);

    public PipelineStatusService(
        IHubContext<PipelineStatusHub, IPipelineStatusHubClient> hubContext,
        IConnectionFactory connectionFactory,
        IDbContextFactory<JaimesDbContext> dbContextFactory,
        ILogger<PipelineStatusService> logger)
    {
        _hubContext = hubContext;
        _connectionFactory = connectionFactory;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<PipelineStatusNotification> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from broker for fresh data
        return await GetQueueSizesFromBrokerAsync(cancellationToken);
    }

    public async Task UpdateQueueSizeAsync(string stage, int queueSize, string? workerSource = null, CancellationToken cancellationToken = default)
    {
        _queueSizes[stage.ToLowerInvariant()] = queueSize;
        _lastUpdate = DateTimeOffset.UtcNow;

        _logger.LogDebug("Pipeline {Stage} queue size updated to {QueueSize} by {WorkerSource}",
            stage, queueSize, workerSource ?? "unknown");

        // Broadcast the updated status to all subscribed clients
        PipelineStatusNotification notification = await GetCurrentStatusAsync(cancellationToken);
        notification = notification with { WorkerSource = workerSource };

        await _hubContext.Clients.Group("pipeline-status").PipelineStatusUpdated(notification);
    }

    public async Task<PipelineStatusNotification> GetQueueSizesFromBrokerAsync(CancellationToken cancellationToken = default)
    {
        int crackingQueueSize = 0;
        int chunkingQueueSize = 0;
        int embeddingQueueSize = 0;
        int readyCount = 0;

        try
        {
            // Query RabbitMQ for queue sizes
            using IConnection connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using IChannel channel = await connection.CreateChannelAsync();

            // Get queue sizes using passive declaration (doesn't create the queue)
            try
            {
                QueueDeclareOk? crackingQueue = await channel.QueueDeclarePassiveAsync(CrackingQueueName, cancellationToken);
                crackingQueueSize = (int)(crackingQueue?.MessageCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Cracking queue not found or not accessible");
            }

            try
            {
                // Create a new channel since the previous one might be closed after an exception
                using IChannel channel2 = await connection.CreateChannelAsync();
                QueueDeclareOk? chunkingQueue = await channel2.QueueDeclarePassiveAsync(ChunkingQueueName, cancellationToken);
                chunkingQueueSize = (int)(chunkingQueue?.MessageCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Chunking queue not found or not accessible");
            }

            try
            {
                using IChannel channel3 = await connection.CreateChannelAsync();
                QueueDeclareOk? embeddingQueue = await channel3.QueueDeclarePassiveAsync(EmbeddingQueueName, cancellationToken);
                embeddingQueueSize = (int)(embeddingQueue?.MessageCount ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Embedding queue not found or not accessible");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get queue sizes from RabbitMQ broker. Using cached values.");

            // Fall back to cached values
            _queueSizes.TryGetValue("cracking", out crackingQueueSize);
            _queueSizes.TryGetValue("chunking", out chunkingQueueSize);
            _queueSizes.TryGetValue("embedding", out embeddingQueueSize);
        }

        // Get ready count from database (documents that are fully processed)
        int totalChunks = 0;
        int totalEmbeddings = 0;
        try
        {
            await using JaimesDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Count documents where IsProcessed is true (all chunks have been embedded)
            readyCount = await dbContext.CrackedDocuments
                .AsNoTracking()
                .Where(d => d.IsProcessed && d.TotalChunks > 0)
                .CountAsync(cancellationToken);

            // Get total chunks and embeddings
            totalChunks = await dbContext.DocumentChunks
                .AsNoTracking()
                .CountAsync(cancellationToken);

            totalEmbeddings = await dbContext.DocumentChunks
                .AsNoTracking()
                .Where(c => c.QdrantPointId != null)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get ready document count from database");
        }

        return new PipelineStatusNotification
        {
            CrackingQueueSize = crackingQueueSize,
            ChunkingQueueSize = chunkingQueueSize,
            EmbeddingQueueSize = embeddingQueueSize,
            ReadyCount = readyCount,
            TotalChunks = totalChunks,
            TotalEmbeddings = totalEmbeddings,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
