using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Agents
{
    private AgentResponse[]? _agents;
    private int? _totalVersions;
    private int? _totalFeedback;
    private double? _averageEvaluation;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: null, disabled: true)
        };
        await LoadAgentsAsync();
    }

    private async Task LoadAgentsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            AgentListResponse? resp = await Http.GetFromJsonAsync<AgentListResponse>("/agents");
            _agents = resp?.Agents ?? [];
            _totalVersions = resp?.TotalVersions;
            _totalFeedback = resp?.TotalFeedback;
            _averageEvaluation = resp?.AverageEvaluation;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Agents").LogError(ex, "Failed to load agents from API");
            _errorMessage = "Failed to load agents: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}

