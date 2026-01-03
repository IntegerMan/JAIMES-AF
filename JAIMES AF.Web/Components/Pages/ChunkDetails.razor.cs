namespace MattEland.Jaimes.Web.Components.Pages;

public partial class ChunkDetails
{
    [Parameter] public string ChunkId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private ChunkDetailsResponse? _response;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: "/admin/rag-collections"),
            new BreadcrumbItem("Document Chunks", href: null),
            new BreadcrumbItem("Chunk Details", href: null, disabled: true)
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
            string url = $"/admin/chunks/{Uri.EscapeDataString(ChunkId)}";
            _response = await Http.GetFromJsonAsync<ChunkDetailsResponse>(url);

            // Update breadcrumbs with document link
            if (_response != null && _breadcrumbs.Count >= 4)
            {
                _breadcrumbs[3] = new BreadcrumbItem(
                    $"{_response.DocumentName} Chunks",
                    href: $"/admin/documents/{_response.DocumentId}/chunks");
                _breadcrumbs[4] = new BreadcrumbItem(
                    $"Chunk {_response.ChunkIndex}",
                    href: null,
                    disabled: true);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("ChunkDetails").LogError(ex, "Failed to load chunk details");
            _errorMessage = "Failed to load chunk details: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetDocumentLink()
    {
        return $"/admin/documents/{_response?.DocumentId}/chunks";
    }

    private string GetQueryLink(Guid queryId)
    {
        // Link to the queries page - note: there's no direct query details page,
        // but we link to the index's queries page
        string indexName = _response?.DocumentKind.ToLowerInvariant() switch
        {
            "sourcebook" => "rules",
            _ => "rules"
        };
        return $"/admin/rag-collections/{indexName}/queries";
    }
}
