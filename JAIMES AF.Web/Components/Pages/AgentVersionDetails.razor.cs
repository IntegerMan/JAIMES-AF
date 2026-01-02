

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class AgentVersionDetails
{
    [Parameter] public string? AgentId { get; set; }
    [Parameter] public int VersionId { get; set; }

    private AgentInstructionVersionResponse? _version;
    private string? _agentName;
    private bool _isLoading = true;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    private int? _previousVersionId;
    private int? _nextVersionId;

    protected override async Task OnParametersSetAsync()
    {
        // Re-construct breadcrumbs on parameter change (or let LoadData handle it)
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem("Agent", href: $"/agents/{AgentId}"),
            new BreadcrumbItem("Version Details", href: null, disabled: true)
        };

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (string.IsNullOrEmpty(AgentId))
        {
            _errorMessage = "Agent ID is required";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _errorMessage = null;

        try
        {
            // Load agent details for name
            var agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{AgentId}");
            _agentName = agent?.Name;

            // Load all versions to determine navigation
            var versionsResp = await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                $"/agents/{AgentId}/instruction-versions");

            var versions = versionsResp?.InstructionVersions?.OrderBy(v => v.CreatedAt).ToList() ??
                           new List<AgentInstructionVersionResponse>();

            _version = versions.FirstOrDefault(v => v.Id == VersionId);

            if (_version != null)
            {
                // Calculate Previous (Older) and Next (Newer)
                // versions is ordered by CreatedAt (Oldest first)
                var currentIndex = versions.IndexOf(_version);

                if (currentIndex > 0)
                {
                    _previousVersionId = versions[currentIndex - 1].Id;
                }
                else
                {
                    _previousVersionId = null;
                }

                if (currentIndex < versions.Count - 1)
                {
                    _nextVersionId = versions[currentIndex + 1].Id;
                }
                else
                {
                    _nextVersionId = null;
                }

                if (_agentName != null)
                {
                    // Update breadcrumbs with proper names
                    _breadcrumbs = new List<BreadcrumbItem>
                    {
                        new BreadcrumbItem("Home", href: "/"),
                        new BreadcrumbItem("Admin", href: "/admin"),
                        new BreadcrumbItem("Agents", href: "/agents"),
                        new BreadcrumbItem(_agentName, href: $"/agents/{AgentId}"),
                        new BreadcrumbItem($"Version {_version.VersionNumber}", href: null, disabled: true)
                    };
                }
            }
            else
            {
                _errorMessage = "Agent version not found.";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("AgentVersionDetails")
                .LogError(ex, "Failed to load agent version details from API");
            _errorMessage = "Failed to load version details: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // Tab and insights tracking
    private int _activeTabIndex;
    private bool _isGeneratingInsights;
    private MattEland.Jaimes.Web.Components.Agents.AgentMessagesList? _messagesGrid;
    private MattEland.Jaimes.Web.Components.Shared.FeedbackGrid? _feedbackGrid;
    private MattEland.Jaimes.Web.Components.Shared.ToolUsageGrid? _toolUsageGrid;
    private MattEland.Jaimes.Web.Components.Shared.MetricsGrid? _metricsGrid;
    private MattEland.Jaimes.Web.Components.Shared.SentimentGrid? _sentimentGrid;

    private async Task GenerateInsightsForActiveTabAsync()
    {
        _isGeneratingInsights = true;
        StateHasChanged();

        try
        {
            switch (_activeTabIndex)
            {
                case 0: // Messages
                    if (_messagesGrid != null)
                        await _messagesGrid.GenerateInsightsAsync();
                    break;
                case 1: // Feedback
                    if (_feedbackGrid != null)
                        await _feedbackGrid.GenerateInsightsAsync();
                    break;
                case 2: // Tool Usage
                    if (_toolUsageGrid != null)
                        await _toolUsageGrid.GenerateInsightsAsync();
                    break;
                case 3: // Metrics
                    if (_metricsGrid != null)
                        await _metricsGrid.GenerateInsightsAsync();
                    break;
                case 4: // Sentiment
                    if (_sentimentGrid != null)
                        await _sentimentGrid.GenerateInsightsAsync();
                    break;
            }
        }
        finally
        {
            _isGeneratingInsights = false;
            StateHasChanged();
        }
    }
}
