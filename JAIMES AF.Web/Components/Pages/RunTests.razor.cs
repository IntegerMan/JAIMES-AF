using MattEland.Jaimes.ServiceDefinitions.Responses;
using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RunTests
{
    [SupplyParameterFromQuery] [Parameter] public int? TestCaseId { get; set; }

    private List<TestCaseResponse>? _testCases;
    private List<AgentWithVersions>? _agents;
    private HashSet<int> _selectedTestCases = [];
    private HashSet<int> _selectedVersions = [];
    private bool _isLoading = true;
    private bool _isRunning = false;
    private int _runProgress = 0;
    private int _totalRuns = 0;

    private List<BreadcrumbItem> _breadcrumbs = new()
    {
        new BreadcrumbItem("Home", href: "/"),
        new BreadcrumbItem("Admin", href: "/admin"),
        new BreadcrumbItem("Test Cases", href: "/admin/test-cases"),
        new BreadcrumbItem("Run Tests", href: null, disabled: true)
    };

    private bool CanRun => _selectedTestCases.Count > 0 && _selectedVersions.Count > 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        try
        {
            // Load test cases
            _testCases = await Http.GetFromJsonAsync<List<TestCaseResponse>>("/test-cases");

            // Load agents with versions
            var agentsResponse = await Http.GetFromJsonAsync<AgentListResponse>("/agents");
            _agents = [];

            if (agentsResponse?.Agents != null)
            {
                foreach (var agent in agentsResponse.Agents)
                {
                    var versionsResp = await Http.GetFromJsonAsync<AgentInstructionVersionListResponse>(
                        $"/agents/{agent.Id}/instruction-versions");

                    _agents.Add(new AgentWithVersions
                    {
                        Id = agent.Id,
                        Name = agent.Name,
                        Versions = versionsResp?.InstructionVersions?.ToList() ?? []
                    });
                }
            }

            // Pre-select test case if provided via query param
            if (TestCaseId.HasValue && _testCases?.Any(tc => tc.Id == TestCaseId.Value) == true)
            {
                _selectedTestCases.Add(TestCaseId.Value);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RunTests").LogError(ex, "Failed to load data");
            Snackbar.Add("Failed to load data: " + ex.Message, Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleTestCase(int id, bool selected)
    {
        if (selected) _selectedTestCases.Add(id);
        else _selectedTestCases.Remove(id);
    }

    private void ToggleVersion(int id, bool selected)
    {
        if (selected) _selectedVersions.Add(id);
        else _selectedVersions.Remove(id);
    }

    private void SelectAllTestCases()
    {
        if (_testCases != null)
        {
            foreach (var tc in _testCases)
                _selectedTestCases.Add(tc.Id);
        }
    }

    private void SelectNoTestCases() => _selectedTestCases.Clear();

    private void SelectAllVersions()
    {
        if (_agents != null)
        {
            foreach (var agent in _agents)
            foreach (var version in agent.Versions)
                _selectedVersions.Add(version.Id);
        }
    }

    private void SelectNoVersions() => _selectedVersions.Clear();

    private async Task RunTestsAsync()
    {
        if (!CanRun) return;

        _isRunning = true;
        _totalRuns = _selectedVersions.Count;
        _runProgress = 0;
        StateHasChanged();

        string executionName = $"multi-test-{DateTime.UtcNow:yyyyMMddHHmmss}";
        List<string> executionNames = [];

        try
        {
            // Run tests against each selected version
            foreach (var versionId in _selectedVersions)
            {
                var version = _agents?
                    .SelectMany(a => a.Versions)
                    .FirstOrDefault(v => v.Id == versionId);

                string? agentId = _agents?
                    .FirstOrDefault(a => a.Versions.Any(v => v.Id == versionId))?.Id;

                if (string.IsNullOrEmpty(agentId)) continue;

                var versionExecutionName = $"{executionName}-v{versionId}";

                var request = new
                {
                    ExecutionName = versionExecutionName,
                    TestCaseIds = _selectedTestCases.ToList()
                };

                var response = await Http.PostAsJsonAsync(
                    $"/agents/{agentId}/versions/{versionId}/test-run", request);

                if (response.IsSuccessStatusCode)
                {
                    executionNames.Add(versionExecutionName);
                }

                _runProgress++;
                StateHasChanged();
            }

            if (executionNames.Count > 0)
            {
                Snackbar.Add($"Completed {executionNames.Count} test run(s)", Severity.Success);

                // Navigate to comparison page with all execution names
                var execParam = string.Join(",", executionNames);
                NavigationManager.NavigateTo($"/admin/test-runs/compare?executions={Uri.EscapeDataString(execParam)}");
            }
            else
            {
                Snackbar.Add("No test runs completed", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RunTests").LogError(ex, "Failed to run tests");
            Snackbar.Add("Failed to run tests: " + ex.Message, Severity.Error);
        }
        finally
        {
            _isRunning = false;
            StateHasChanged();
        }
    }

    private record AgentWithVersions
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public List<AgentInstructionVersionResponse> Versions { get; init; } = [];
    }
}
