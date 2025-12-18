using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditScenario
{
    [Parameter] public string ScenarioId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    [Inject] public IDialogService DialogService { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string? _selectedRulesetId;
    private string _name = string.Empty;
    private string? _description;
    private string? _scenarioInstructions;
    private string? _initialGreeting;
    private AgentResponse[] _allAgents = [];
    private ScenarioAgentWithDetails[] _scenarioAgents = [];
    private Dictionary<string, AgentInstructionVersionResponse[]> _agentVersions = new();
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;

    private record ScenarioAgentWithDetails
    {
        public required string ScenarioId { get; init; }
        public required string AgentId { get; init; }
        public required string AgentName { get; init; }
        public required string AgentRole { get; init; }
        public required int InstructionVersionId { get; init; }
        public required string VersionNumber { get; init; }
        public required string Instructions { get; init; }
        public required bool IsActive { get; init; }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            Task<RulesetListResponse?> rulesetsTask = Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            Task<ScenarioResponse?> scenarioTask = Http.GetFromJsonAsync<ScenarioResponse>($"/scenarios/{ScenarioId}");
            Task<ScenarioAgentListResponse?> scenarioAgentsTask = Http.GetFromJsonAsync<ScenarioAgentListResponse>($"/scenarios/{ScenarioId}/agents");
            Task<AgentListResponse?> agentsTask = Http.GetFromJsonAsync<AgentListResponse>("/agents");

            await Task.WhenAll(rulesetsTask, scenarioTask, scenarioAgentsTask, agentsTask);

            RulesetListResponse? rulesetsResponse = await rulesetsTask;
            ScenarioResponse? scenarioResponse = await scenarioTask;
            ScenarioAgentListResponse? scenarioAgentsResponse = await scenarioAgentsTask;
            AgentListResponse? agentsResponse = await agentsTask;

            if (scenarioResponse == null)
            {
                _errorMessage = $"Scenario with ID '{ScenarioId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _rulesets = rulesetsResponse?.Rulesets ?? [];
            _allAgents = agentsResponse?.Agents ?? [];
            _selectedRulesetId = scenarioResponse.RulesetId;
            _name = scenarioResponse.Name;
            _description = scenarioResponse.Description;
            _scenarioInstructions = scenarioResponse.ScenarioInstructions;
            _initialGreeting = scenarioResponse.InitialGreeting;

            // Load scenario agents with details
            await LoadScenarioAgentsWithDetailsAsync(scenarioAgentsResponse?.ScenarioAgents ?? []);
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, "Failed to load scenario or rulesets from API");
            _errorMessage = "Failed to load scenario: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadScenarioAgentsWithDetailsAsync(ScenarioAgentResponse[] scenarioAgentResponses)
    {
        var scenarioAgentsWithDetails = new List<ScenarioAgentWithDetails>();

        // First, load all instruction versions for all agents used in this scenario
        var agentIds = scenarioAgentResponses.Select(sa => sa.AgentId).Distinct().ToArray();
        foreach (var agentId in agentIds)
        {
            try
            {
                var versions = await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>($"/agents/{agentId}/instruction-versions");
                if (versions != null)
                {
                    _agentVersions[agentId] = versions.InstructionVersions;
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("EditScenario").LogError(ex, $"Failed to load instruction versions for agent {agentId}");
                _agentVersions[agentId] = [];
            }
        }

        foreach (var scenarioAgent in scenarioAgentResponses)
        {
            // Get agent details
            var agent = _allAgents.FirstOrDefault(a => a.Id == scenarioAgent.AgentId);
            if (agent == null) continue;

            // Get instruction version details
            var versions = _agentVersions.GetValueOrDefault(scenarioAgent.AgentId, []);
            var version = versions.FirstOrDefault(v => v.Id == scenarioAgent.InstructionVersionId);

            if (version != null)
            {
                scenarioAgentsWithDetails.Add(new ScenarioAgentWithDetails
                {
                    ScenarioId = scenarioAgent.ScenarioId,
                    AgentId = scenarioAgent.AgentId,
                    AgentName = agent.Name,
                    AgentRole = agent.Role,
                    InstructionVersionId = scenarioAgent.InstructionVersionId,
                    VersionNumber = version.VersionNumber,
                    Instructions = version.Instructions,
                    IsActive = version.IsActive
                });
            }
        }

        _scenarioAgents = scenarioAgentsWithDetails.ToArray();
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(_name);
        // ScenarioInstructions is optional
    }

    private async Task UpdateScenarioAsync()
    {
        if (!IsFormValid())
        {
            _errorMessage = "Please fill in all required fields.";
            StateHasChanged();
            return;
        }

        _isSaving = true;
        _errorMessage = null;
        try
        {
            UpdateScenarioRequest request = new()
            {
                RulesetId = _selectedRulesetId!,
                Description = _description,
                Name = _name,
                ScenarioInstructions = _scenarioInstructions,
                InitialGreeting = _initialGreeting
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/scenarios/{ScenarioId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/scenarios");
            }
            else
            {
                string? body = null;
                try
                {
                    body = await response.Content.ReadAsStringAsync();
                }
                catch
                {
                    // ignored
                }

                _errorMessage =
                    $"Failed to update scenario: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, "Failed to update scenario");
            _errorMessage = "Failed to update scenario: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private async Task AddAgentAsync()
    {
        // Get available agents (not already assigned to this scenario)
        var availableAgents = _allAgents.Where(a => !_scenarioAgents.Any(sa => sa.AgentId == a.Id)).ToArray();

        if (availableAgents.Length == 0)
        {
            _errorMessage = "All available agents are already assigned to this scenario.";
            StateHasChanged();
            return;
        }

        // For now, we'll use a simple approach - assign the first available agent with their active instruction version
        // In a more sophisticated UI, you'd have a dialog to select agent and version
        var agentToAdd = availableAgents.First();

        try
        {
            // Get the active instruction version for this agent
            var activeVersion = await Http.GetFromJsonAsync<AgentInstructionVersionResponse>($"/agents/{agentToAdd.Id}/instruction-versions/active");
            if (activeVersion == null)
            {
                _errorMessage = $"No active instruction version found for agent '{agentToAdd.Name}'.";
                StateHasChanged();
                return;
            }

            // Add the agent to the scenario
            var request = new SetScenarioAgentRequest
            {
                AgentId = agentToAdd.Id,
                InstructionVersionId = activeVersion.Id
            };

            var response = await Http.PostAsJsonAsync($"/scenarios/{ScenarioId}/agents", request);
            if (response.IsSuccessStatusCode)
            {
                await LoadDataAsync(); // Reload to show the new agent
            }
            else
            {
                _errorMessage = "Failed to add agent to scenario.";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, $"Failed to add agent {agentToAdd.Id} to scenario");
            _errorMessage = "Failed to add agent: " + ex.Message;
            StateHasChanged();
        }
    }

    private void ChangeAgentVersion(ScenarioAgentWithDetails scenarioAgent, int newVersionId)
    {
        _ = ChangeAgentVersionAsync(scenarioAgent, newVersionId);
    }

    private async Task ChangeAgentVersionAsync(ScenarioAgentWithDetails scenarioAgent, int newVersionId)
    {
        try
        {
            var request = new SetScenarioAgentRequest
            {
                AgentId = scenarioAgent.AgentId,
                InstructionVersionId = newVersionId
            };

            var response = await Http.PutAsJsonAsync($"/scenarios/{ScenarioId}/agents/{scenarioAgent.AgentId}", request);
            if (response.IsSuccessStatusCode)
            {
                await LoadDataAsync(); // Reload to show updated version
            }
            else
            {
                _errorMessage = "Failed to change agent version.";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, $"Failed to change version for agent {scenarioAgent.AgentId}");
            _errorMessage = "Failed to change agent version: " + ex.Message;
            StateHasChanged();
        }
    }

    private async Task RemoveAgentAsync(ScenarioAgentWithDetails scenarioAgent)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Remove Agent",
            $"Are you sure you want to remove agent '{scenarioAgent.AgentName}' from this scenario?",
            yesText: "Remove", cancelText: "Cancel");

        if (result == true)
        {
            try
            {
                var response = await Http.DeleteAsync($"/scenarios/{ScenarioId}/agents/{scenarioAgent.AgentId}");
                if (response.IsSuccessStatusCode)
                {
                    await LoadDataAsync(); // Reload to show removed agent
                }
                else
                {
                    _errorMessage = "Failed to remove agent from scenario.";
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("EditScenario").LogError(ex, $"Failed to remove agent {scenarioAgent.AgentId} from scenario");
                _errorMessage = "Failed to remove agent: " + ex.Message;
                StateHasChanged();
            }
        }
    }

    private ICollection<AgentInstructionVersionResponse> GetAgentInstructionVersions(string agentId)
    {
        return _agentVersions.GetValueOrDefault(agentId, []);
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/scenarios");
    }
}