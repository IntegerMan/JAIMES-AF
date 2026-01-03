using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EvaluatorTest
{
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    // State
    private bool _isLoading = true;
    private bool _isEvaluating = false;
    private string? _errorMessage;

    // Data
    private AgentResponse[] _agents = [];
    private readonly Dictionary<string, AgentInstructionVersionResponse[]> _agentVersions = new();
    private readonly Dictionary<int, AgentInstructionVersionResponse> _versionLookup = new();
    private readonly List<AgentVersionOption> _versionOptions = [];
    private RulesetInfoResponse[] _rulesets = [];
    private List<string> _availableEvaluators = [];

    // Form state
    private int _selectedVersionId;
    private AgentInstructionVersionResponse? _selectedVersion;
    private string? _selectedRulesetId;
    private IEnumerable<string> _selectedEvaluators = [];
    private List<ConversationMessage> _conversationMessages = [];
    private string _assistantResponse = string.Empty;

    // Results
    private TestEvaluatorResponse? _evaluationResult;

    private List<BreadcrumbItem> _breadcrumbs = [];

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs =
        [
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Evaluators", href: "/admin/evaluators"),
            new BreadcrumbItem("Test", href: null, disabled: true)
        ];

        await LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            // Load agents, rulesets, and available evaluators in parallel
            Task<AgentListResponse?> agentsTask = Http.GetFromJsonAsync<AgentListResponse>("/agents");
            Task<RulesetListResponse?> rulesetsTask = Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            Task<AvailableEvaluatorsResponse?> evaluatorsTask = Http.GetFromJsonAsync<AvailableEvaluatorsResponse>("/admin/evaluators/available");

            await Task.WhenAll(agentsTask, rulesetsTask, evaluatorsTask);

            _agents = agentsTask.Result?.Agents ?? [];
            _rulesets = rulesetsTask.Result?.Rulesets ?? [];
            _availableEvaluators = evaluatorsTask.Result?.EvaluatorNames ?? [];

            await LoadAllVersionsAsync();

            // Default to first ruleset if available
            if (_rulesets.Length > 0)
            {
                _selectedRulesetId = _rulesets[0].Id;
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EvaluatorTest").LogError(ex, "Failed to load initial data");
            _errorMessage = "Failed to load data: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string GetVersionDisplayText(int versionId)
    {
        if (versionId == 0)
        {
            return string.Empty;
        }

        AgentVersionOption? option = _versionOptions.FirstOrDefault(v => v.VersionId == versionId);
        return option?.Display ?? versionId.ToString();
    }

    private string GetRulesetDisplayText(string? rulesetId)
    {
        if (string.IsNullOrEmpty(rulesetId))
        {
            return string.Empty;
        }

        RulesetInfoResponse? ruleset = _rulesets.FirstOrDefault(r => r.Id == rulesetId);
        return ruleset?.Name ?? rulesetId;
    }

    private int SelectedVersionId
    {
        get => _selectedVersionId;
        set
        {
            if (_selectedVersionId != value)
            {
                _selectedVersionId = value;
                if (_versionLookup.TryGetValue(value, out AgentInstructionVersionResponse? version))
                {
                    _selectedVersion = version;
                }
            }
        }
    }

    private async Task LoadAllVersionsAsync()
    {
        _agentVersions.Clear();
        _versionLookup.Clear();
        _versionOptions.Clear();

        foreach (AgentResponse agent in _agents)
        {
            try
            {
                AgentInstructionVersionListResponse? response =
                    await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                        $"/agents/{agent.Id}/instruction-versions");

                AgentInstructionVersionResponse[] versions = response?.InstructionVersions ?? [];
                _agentVersions[agent.Id] = versions;

                foreach (AgentInstructionVersionResponse version in versions)
                {
                    _versionLookup[version.Id] = version;
                    _versionOptions.Add(new AgentVersionOption(
                        version.Id,
                        agent.Id,
                        agent.Name,
                        $"{agent.Name} - {version.VersionNumber}{(version.IsActive ? " (Active)" : string.Empty)}",
                        version.IsActive));
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("EvaluatorTest").LogError(ex, "Failed to load versions for agent {AgentId}", agent.Id);
                _errorMessage = "Failed to load instruction versions: " + ex.Message;
            }
        }

        AgentVersionOption? activeOption = _versionOptions.FirstOrDefault(v => v.IsActive);
        AgentVersionOption? firstOption = _versionOptions.FirstOrDefault();

        if (activeOption != null)
        {
            _selectedVersionId = activeOption.VersionId;
            _versionLookup.TryGetValue(_selectedVersionId, out _selectedVersion);
        }
        else if (firstOption != null)
        {
            _selectedVersionId = firstOption.VersionId;
            _versionLookup.TryGetValue(_selectedVersionId, out _selectedVersion);
        }
    }

    private void AddUserMessage()
    {
        _conversationMessages.Add(new ConversationMessage { Role = "user", Text = "" });
    }

    private void AddAssistantMessage()
    {
        _conversationMessages.Add(new ConversationMessage { Role = "assistant", Text = "" });
    }

    private void RemoveMessage(int index)
    {
        if (index >= 0 && index < _conversationMessages.Count)
        {
            _conversationMessages.RemoveAt(index);
        }
    }

    private bool CanRunEvaluation()
    {
        return !_isEvaluating
               && _selectedVersionId > 0
               && !string.IsNullOrWhiteSpace(_assistantResponse);
    }

    private async Task RunEvaluationAsync()
    {
        if (!CanRunEvaluation())
        {
            return;
        }

        _isEvaluating = true;
        _errorMessage = null;
        _evaluationResult = null;
        StateHasChanged();

        try
        {
            TestEvaluatorRequest request = new()
            {
                InstructionVersionId = _selectedVersionId,
                RulesetId = _selectedRulesetId,
                EvaluatorNames = _selectedEvaluators.ToList(),
                ConversationContext = _conversationMessages
                    .Where(m => !string.IsNullOrWhiteSpace(m.Text))
                    .Select(m => new TestEvaluatorMessage
                    {
                        Role = m.Role,
                        Text = m.Text
                    })
                    .ToList(),
                AssistantResponse = _assistantResponse
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/admin/evaluators/test", request);

            if (response.IsSuccessStatusCode)
            {
                _evaluationResult = await response.Content.ReadFromJsonAsync<TestEvaluatorResponse>();
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                _errorMessage = $"Evaluation failed: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EvaluatorTest").LogError(ex, "Failed to run evaluation");
            _errorMessage = "Failed to run evaluation: " + ex.Message;
        }
        finally
        {
            _isEvaluating = false;
            StateHasChanged();
        }
    }

    private static Color GetScoreColor(double score)
    {
        return score switch
        {
            >= 4.5 => Color.Success,
            >= 3.5 => Color.Info,
            >= 2.5 => Color.Warning,
            _ => Color.Error
        };
    }

    private static Severity GetDiagnosticSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => Severity.Error,
            "warning" => Severity.Warning,
            _ => Severity.Info
        };
    }

    private class ConversationMessage
    {
        public string Role { get; set; } = "user";
        public string Text { get; set; } = "";
    }

    private sealed record AgentVersionOption(int VersionId, string AgentId, string AgentName, string Display, bool IsActive);
}
