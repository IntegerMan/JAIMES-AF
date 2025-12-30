namespace MattEland.Jaimes.ServiceDefinitions.Responses;

public class FeedbackListResponse
{
    public IEnumerable<FeedbackListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
