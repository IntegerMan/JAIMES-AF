namespace MattEland.Jaimes.Web.Components.Pages;

public partial class GameTranscriptChunks
{
    [Parameter] public Guid GameId { get; set; }

    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private TranscriptChunksResponse? _response;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private int _currentPage = 1;

    private int TotalPages => _response == null
        ? 1
        : (int)Math.Ceiling((double)_response.TotalCount / _response.PageSize);

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: "/admin/rag-collections"),
            new BreadcrumbItem("Transcript Messages", href: null, disabled: true)
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        _currentPage = 1;
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            string url = $"/admin/games/{GameId}/transcript-chunks?page={_currentPage}&pageSize=25";
            _response = await Http.GetFromJsonAsync<TranscriptChunksResponse>(url);

            // Update breadcrumb with game title
            if (_response != null && _breadcrumbs.Count > 3)
            {
                _breadcrumbs[3] = new BreadcrumbItem($"{_response.GameTitle} Messages", href: null, disabled: true);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("GameTranscriptChunks").LogError(ex, "Failed to load transcript chunks");
            _errorMessage = "Failed to load messages: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnPageChanged(int page)
    {
        _currentPage = page;
        await LoadDataAsync();
    }

    private static string GetMessageDetailsLink(int messageId)
    {
        return $"/admin/transcript-messages/{messageId}";
    }
}
