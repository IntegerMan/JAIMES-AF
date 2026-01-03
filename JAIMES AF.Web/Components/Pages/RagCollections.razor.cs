namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RagCollections
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private RagCollectionStatisticsResponse? _statistics;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private string _filterType = "All";

    private RagCollectionDocumentInfo[] FilteredDocuments =>
        _statistics?.Documents
            .Where(d => _filterType == "All" ||
                        (_filterType == "Sourcebook" && d.DocumentKind == "Sourcebook") ||
                        (_filterType == "Transcript" && d.DocumentKind == "Transcript"))
            .ToArray() ?? [];

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: null, disabled: true)
        };

        await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            _statistics = await Http.GetFromJsonAsync<RagCollectionStatisticsResponse>("/admin/rag-collections");
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RagCollections").LogError(ex, "Failed to load RAG collection statistics");
            _errorMessage = "Failed to load statistics: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetFilter(string filterType)
    {
        _filterType = filterType;
    }

    private static string GetQueryLink(string collectionType)
    {
        string indexName = collectionType.ToLowerInvariant() switch
        {
            "sourcebook" => "rules",
            "transcript" => "conversations",
            _ => collectionType.ToLowerInvariant()
        };
        return $"/admin/rag-collections/{indexName}/queries";
    }

    private static string GetDocumentQueryLink(string collectionType, string documentName)
    {
        string indexName = collectionType.ToLowerInvariant() switch
        {
            "sourcebook" => "rules",
            "transcript" => "conversations",
            _ => collectionType.ToLowerInvariant()
        };
        return $"/admin/rag-collections/{indexName}/queries?documentName={Uri.EscapeDataString(documentName)}";
    }
}
