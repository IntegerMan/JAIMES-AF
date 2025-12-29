using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class ConversationSearchService(
    ILogger<ConversationSearchService> logger,
    IQdrantConversationsStore conversationsStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IDbContextFactory<JaimesDbContext> dbContextFactory) : IConversationSearchService
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.Agents.ConversationSearch");

    public async Task<ConversationSearchResponse> SearchConversationsAsync(
        Guid gameId,
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        logger.LogInformation("Searching conversations for game {GameId} with query: {Query}", gameId, query);

        // Create OpenTelemetry activity for the overall search operation
        using Activity? activity = ActivitySource.StartActivity("ConversationSearch.Search");
        if (activity != null)
        {
            activity.SetTag("game.id", gameId.ToString());
            activity.SetTag("search.query", query);
            activity.SetTag("search.limit", limit);
        }

        try
        {
            // Generate embedding for the query
            GeneratedEmbeddings<Embedding<float>> embeddings =
                await embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            float[] queryEmbedding = embeddings[0].Vector.ToArray();

            // Search for similar conversations in Qdrant filtered by gameId
            List<ConversationSearchHit> qdrantResults = await conversationsStore.SearchConversationsAsync(
                queryEmbedding,
                gameId,
                limit,
                cancellationToken);

            if (activity != null)
            {
                activity.SetTag("search.result_count", qdrantResults.Count);
            }

            if (qdrantResults.Count == 0)
            {
                logger.LogWarning("No results found for query: {Query} in game: {GameId}", query, gameId);
                return new ConversationSearchResponse { Results = [] };
            }

            logger.LogInformation("Found {Count} results for query: {Query} in game: {GameId}",
                qdrantResults.Count,
                query,
                gameId);

            // Load messages from PostgreSQL with context (previous and next messages)
            await using JaimesDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            List<ServiceDefinitions.Responses.ConversationSearchResult> results = new();
            foreach (ConversationSearchHit qdrantResult in qdrantResults)
            {
                // Load the matched message with navigation properties
                Message? matchedMessage = await dbContext.Messages
                    .Include(m => m.Player)
                    .Include(m => m.PreviousMessage)
                        .ThenInclude(m => m!.Player)
                    .Include(m => m.NextMessage)
                        .ThenInclude(m => m!.Player)
                    .FirstOrDefaultAsync(m => m.Id == qdrantResult.MessageId, cancellationToken);

                if (matchedMessage == null)
                {
                    logger.LogWarning("Message {MessageId} from Qdrant search not found in PostgreSQL", qdrantResult.MessageId);
                    continue;
                }

                // Convert to DTOs manually (avoiding circular dependency with Services project)
                MessageDto matchedDto = ConvertToDto(matchedMessage);
                MessageDto? previousDto = matchedMessage.PreviousMessage != null ? ConvertToDto(matchedMessage.PreviousMessage) : null;
                MessageDto? nextDto = matchedMessage.NextMessage != null ? ConvertToDto(matchedMessage.NextMessage) : null;

                // Convert to response format
                MessageResponse matchedResponse = ConvertToResponse(matchedDto);
                MessageResponse? previousResponse = previousDto != null ? ConvertToResponse(previousDto) : null;
                MessageResponse? nextResponse = nextDto != null ? ConvertToResponse(nextDto) : null;

                results.Add(new ServiceDefinitions.Responses.ConversationSearchResult
                {
                    MatchedMessage = matchedResponse,
                    PreviousMessage = previousResponse,
                    NextMessage = nextResponse,
                    Relevancy = qdrantResult.Score
                });
            }

            if (activity != null)
            {
                activity.SetTag("search.final_result_count", results.Count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            logger.LogInformation("Search completed - Results: {ResultCount}", results.Count);

            return new ConversationSearchResponse { Results = results.ToArray() };
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            logger.LogError(ex, "Failed to search conversations for game {GameId}", gameId);
            throw;
        }
    }

    private static MessageDto ConvertToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            Text = message.Text,
            PlayerId = message.PlayerId,
            ParticipantName = message.Player?.Name ?? "Game Master",
            CreatedAt = message.CreatedAt,
            AgentId = message.AgentId,
            InstructionVersionId = message.InstructionVersionId
        };
    }

    private static MessageResponse ConvertToResponse(MessageDto dto)
    {
        return new MessageResponse
        {
            Id = dto.Id,
            Text = dto.Text,
            Participant = string.IsNullOrEmpty(dto.PlayerId) ? ChatParticipant.GameMaster : ChatParticipant.Player,
            PlayerId = dto.PlayerId,
            ParticipantName = dto.ParticipantName,
            CreatedAt = dto.CreatedAt,
            AgentId = dto.AgentId,
            InstructionVersionId = dto.InstructionVersionId
        };
    }
}

