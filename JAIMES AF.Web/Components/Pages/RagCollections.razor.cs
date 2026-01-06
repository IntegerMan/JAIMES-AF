using MattEland.Jaimes.Web.Components.Shared;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RagCollections
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public IDialogService DialogService { get; set; } = null!;

    private bool _isLoading = true;
    private string? _errorMessage;
    private RagCollectionStatisticsResponse? _statistics;
    private List<BreadcrumbItem> _breadcrumbs = new();
    private string _filterType = "All";

    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    [Parameter]
    public string? RulesetId { get; set; }

    private RagCollectionDocumentInfo[] FilteredDocuments =>
        _statistics?.Documents
            .Where(d => (_filterType == "All" ||
                         (_filterType == "Sourcebook" && d.DocumentKind == "Sourcebook") ||
                         (_filterType == "Transcript" && d.DocumentKind == "Transcript")) &&
                        (string.IsNullOrEmpty(RulesetId) || string.Equals(d.RulesetId, RulesetId, StringComparison.OrdinalIgnoreCase)))
            .ToArray() ?? [];

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("RAG Collections", href: string.IsNullOrEmpty(RulesetId) ? null : "/admin/rag-collections", disabled: string.IsNullOrEmpty(RulesetId))
        };

        if (!string.IsNullOrEmpty(RulesetId))
        {
            _breadcrumbs.Add(new BreadcrumbItem(RulesetId, href: null, disabled: true));
        }

        // Initialize filter from query parameter
        if (!string.IsNullOrEmpty(Category))
        {
            _filterType = Category.ToLowerInvariant() switch
            {
                "transcripts" or "transcript" => "Transcript",
                "sourcebooks" or "sourcebook" => "Sourcebook",
                _ => "All"
            };
        }

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
        return $"/admin/rag-collections/{Uri.EscapeDataString(indexName)}/queries";
    }

    private static string GetDocumentQueryLink(string collectionType, string documentName)
    {
        string indexName = collectionType.ToLowerInvariant() switch
        {
            "sourcebook" => "rules",
            "transcript" => "conversations",
            _ => collectionType.ToLowerInvariant()
        };
        return
            $"/admin/rag-collections/{Uri.EscapeDataString(indexName)}/queries?documentName={Uri.EscapeDataString(documentName)}";
    }

    private static string GetChunksLink(RagCollectionDocumentInfo doc)
    {
        // For sourcebooks, link to document chunks page
        // For transcripts, link to game transcript chunks page
        return doc.DocumentKind.ToLowerInvariant() switch
        {
            "transcript" when doc.GameId.HasValue => $"/admin/games/{doc.GameId}/transcript-chunks",
            _ => $"/admin/documents/{doc.DocumentId}/chunks"
        };
    }

    private async Task ViewDocumentAsync(RagCollectionDocumentInfo document)
    {
        // Build full URL to API service for iframe
        string relativePath = $"admin/rag-documents/{document.DocumentId}/file";
        string fileUrl = new Uri(Http.BaseAddress!, relativePath).ToString();

        var parameters = new DialogParameters<DocumentViewerDialog>
        {
            { x => x.FileName, document.FileName },
            { x => x.FileUrl, fileUrl }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseButton = true
        };

        await DialogService.ShowAsync<DocumentViewerDialog>($"View {document.FileName}", parameters, options);
    }
}
