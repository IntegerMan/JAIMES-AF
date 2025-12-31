namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Scenarios
{
    private ScenarioInfoResponse[]? _scenarios;
    private Dictionary<string, List<AgentDisplayInfo>> _scenarioAgents = new();
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Content", href: null, disabled: true),
            new BreadcrumbItem("Scenarios", href: null, disabled: true)
        };
        await LoadScenariosAsync();
    }

    private List<BreadcrumbItem> _breadcrumbs = new();

    private async Task LoadScenariosAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            ScenarioListResponse? resp = await Http.GetFromJsonAsync<ScenarioListResponse>("/scenarios");
            _scenarios = resp?.Scenarios ?? [];

            // Load agent information for each scenario
            foreach (var scenario in _scenarios)
            {
                await LoadScenarioAgentsAsync(scenario.Id);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Scenarios").LogError(ex, "Failed to load scenarios from API");
            _errorMessage = "Failed to load scenarios: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadScenarioAgentsAsync(string scenarioId)
    {
        try
        {
            var agentsResponse =
                await Http.GetFromJsonAsync<ScenarioAgentListResponse>($"/scenarios/{scenarioId}/agents");
            var agentInfos = new List<AgentDisplayInfo>();

            if (agentsResponse?.ScenarioAgents != null)
            {
                foreach (var scenarioAgent in agentsResponse.ScenarioAgents)
                {
                    // Get agent details
                    var agent = await Http.GetFromJsonAsync<AgentResponse>($"/agents/{scenarioAgent.AgentId}");
                    if (agent != null)
                    {
                        // Get instruction version details
                        var version = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>(
                            $"/agents/{scenarioAgent.AgentId}/instruction-versions/{scenarioAgent.InstructionVersionId}");

                        agentInfos.Add(new AgentDisplayInfo
                        {
                            AgentId = scenarioAgent.AgentId,
                            AgentName = agent.Name,
                            VersionId = scenarioAgent.InstructionVersionId,
                            VersionNumber = version?.VersionNumber ?? "v?"
                        });
                    }
                }
            }

            _scenarioAgents[scenarioId] = agentInfos;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Scenarios").LogError(ex, $"Failed to load agents for scenario {scenarioId}");
            _scenarioAgents[scenarioId] = new List<AgentDisplayInfo>();
        }
    }

    private IEnumerable<AgentDisplayInfo> GetScenarioAgentsDisplay(string scenarioId)
    {
        return _scenarioAgents.GetValueOrDefault(scenarioId, new List<AgentDisplayInfo>());
    }

    /// <summary>
    /// Holds structured agent information for display with links.
    /// </summary>
    public class AgentDisplayInfo
    {
        public required string AgentId { get; init; }
        public required string AgentName { get; init; }
        public int VersionId { get; init; }
        public required string VersionNumber { get; init; }
    }
}
