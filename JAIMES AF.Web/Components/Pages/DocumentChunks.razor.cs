namespace MattEland.Jaimes.Web.Components.Pages;

public partial class DocumentChunks
{
    [Parameter] public int DocumentId { get; set; }

    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private DocumentChunksResponse? _response;
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
            new BreadcrumbItem("Document Chunks", href: null, disabled: true)
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
            string url = $"/admin/documents/{DocumentId}/chunks?page={_currentPage}&pageSize=25";
            _response = await Http.GetFromJsonAsync<DocumentChunksResponse>(url);

            // Update breadcrumb with document name
            if (_response != null && _breadcrumbs.Count > 3)
            {
                _breadcrumbs[3] = new BreadcrumbItem($"{_response.DocumentName} Chunks", href: null, disabled: true);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("DocumentChunks").LogError(ex, "Failed to load document chunks");
            _errorMessage = "Failed to load chunks: " + ex.Message;
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

    private static string GetChunkDetailsLink(string chunkId)
    {
        return $"/admin/chunks/{Uri.EscapeDataString(chunkId)}";
    }
}
