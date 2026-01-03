namespace MattEland.Jaimes.Web.Components.Pages;

public partial class TranscriptMessageDetails
{
    [Parameter] public int MessageId { get; set; }

    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private TranscriptMessageDetailsResponse? _response;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: "/admin/rag-collections"),
            new BreadcrumbItem("Transcript Messages", href: null),
            new BreadcrumbItem("Message Details", href: null, disabled: true)
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            string url = $"/admin/transcript-messages/{MessageId}";
            _response = await Http.GetFromJsonAsync<TranscriptMessageDetailsResponse>(url);

            // Update breadcrumbs with game link
            if (_response != null && _breadcrumbs.Count >= 4)
            {
                _breadcrumbs[3] = new BreadcrumbItem(
                    $"{_response.GameTitle} Messages",
                    href: $"/admin/games/{_response.GameId}/transcript-chunks");
                _breadcrumbs[4] = new BreadcrumbItem(
                    $"Message {_response.MessageId}",
                    href: null,
                    disabled: true);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("TranscriptMessageDetails").LogError(ex, "Failed to load message details");
            _errorMessage = "Failed to load message details: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetGameLink()
    {
        return $"/admin/games/{_response?.GameId}/transcript-chunks";
    }

    private string GetQueryLink()
    {
        return "/admin/rag-collections/conversations/queries";
    }
}
