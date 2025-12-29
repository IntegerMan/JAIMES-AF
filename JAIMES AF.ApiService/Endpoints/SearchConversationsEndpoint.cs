using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

public class SearchConversationsEndpoint : Ep.Req<ConversationSearchRequest>.Res<ConversationSearchResponse>
{
    public required IConversationSearchService ConversationSearchService { get; set; }

    public override void Configure()
    {
        Post("/conversations/search");
        AllowAnonymous();
        Description(b => b
            .Produces<ConversationSearchResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Conversations"));
    }

    public override async Task HandleAsync(ConversationSearchRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
        {
            ThrowError("Query is required");
            return;
        }

        try
        {
            ConversationSearchResponse response = await ConversationSearchService.SearchConversationsAsync(
                req.GameId,
                req.Query,
                req.Limit,
                ct);

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

