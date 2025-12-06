namespace MattEland.Jaimes.ApiService.Endpoints;

public class SearchRulesEndpoint : Ep.Req<SearchRulesRequest>.Res<SearchRulesResponse>
{
    public required IRulesSearchService RulesSearchService { get; set; }

    public override void Configure()
    {
        Post("/rules/search");
        AllowAnonymous();
        Description(b => b
            .Produces<SearchRulesResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Rules"));
    }

    public override async Task HandleAsync(SearchRulesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
        {
            ThrowError("Query is required");
            return;
        }

        try
        {
            SearchRulesResponse response = await RulesSearchService.SearchRulesDetailedAsync(
                req.RulesetId,
                req.Query,
                req.StoreResults,
                ct);

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}