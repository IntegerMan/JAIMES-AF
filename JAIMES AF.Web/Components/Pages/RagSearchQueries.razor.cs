namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RagSearchQueries
{
    [Parameter] public string IndexName { get; set; } = string.Empty;

    [SupplyParameterFromQuery(Name = "documentName")]
    public string? DocumentName { get; set; }

    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;
    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private RagSearchQueriesResponse? _statistics;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private HashSet<Guid> _expandedRows = new();
    private int _currentPage = 1;

    private int TotalPages => _statistics == null
        ? 1
        : (int) Math.Ceiling((double) _statistics.TotalCount / _statistics.PageSize);

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: "/admin/rag-collections"),
            new BreadcrumbItem("Search Queries", href: null, disabled: true)
        };
    }

    protected override async Task OnParametersSetAsync()
    {
        _currentPage = 1; // Reset page when parameters change
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            string url =
                $"/admin/rag-collections/{Uri.EscapeDataString(IndexName)}/queries?page={_currentPage}&pageSize=25";
            if (!string.IsNullOrWhiteSpace(DocumentName))
            {
                url += $"&documentName={Uri.EscapeDataString(DocumentName)}";
            }

            _statistics = await Http.GetFromJsonAsync<RagSearchQueriesResponse>(url);

            // Update breadcrumb with collection name and optional document filter
            if (_statistics != null && _breadcrumbs.Count > 3)
            {
                string title = !string.IsNullOrWhiteSpace(DocumentName)
                    ? $"{DocumentName} Queries"
                    : $"{_statistics.CollectionDisplayName} Queries";
                _breadcrumbs[3] = new BreadcrumbItem(title, href: null, disabled: true);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RagSearchQueries").LogError(ex, "Failed to load RAG search queries");
            _errorMessage = "Failed to load search queries: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleExpanded(Guid queryId)
    {
        if (_expandedRows.Contains(queryId))
            _expandedRows.Remove(queryId);
        else
            _expandedRows.Add(queryId);
    }

    private bool IsExpanded(Guid queryId) => _expandedRows.Contains(queryId);

    private async Task OnPageChanged(int page)
    {
        _currentPage = page;
        _expandedRows.Clear();
        await LoadDataAsync();
    }

    private void ClearDocumentFilter()
    {
        NavigationManager.NavigateTo($"/admin/rag-collections/{IndexName}/queries");
    }

    private string GetChunkDetailsLink(string chunkId)
    {
        // For conversations index, chunk IDs are message IDs (integers)
        // For rules index, chunk IDs are GUIDs
        string normalizedIndex = IndexName.ToLowerInvariant();
        return normalizedIndex switch
        {
            "conversations" when int.TryParse(chunkId, out int messageId)
                => $"/admin/transcript-messages/{messageId}",
            _ => $"/admin/chunks/{Uri.EscapeDataString(chunkId)}"
        };
    }
}
