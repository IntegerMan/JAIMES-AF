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
        StateHasChanged();

        string executionName = $"multi-test-{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            // Build the list of versions to test
            var versionsToTest = new List<object>();
            foreach (var versionId in _selectedVersions)
            {
                string? agentId = _agents?
                    .FirstOrDefault(a => a.Versions.Any(v => v.Id == versionId))?.Id;

                if (!string.IsNullOrEmpty(agentId))
                {
                    versionsToTest.Add(new
                    {
                        AgentId = agentId,
                        InstructionVersionId = versionId
                    });
                }
            }

            if (versionsToTest.Count == 0)
            {
                Snackbar.Add("No valid versions selected", Severity.Warning);
                return;
            }

            // Single API call to run all versions
            var request = new
            {
                Versions = versionsToTest,
                TestCaseIds = _selectedTestCases.ToList(),
                ExecutionName = executionName
            };

            var response = await Http.PostAsJsonAsync("/test-runs/multi-version", request);

            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add($"Completed test runs for {versionsToTest.Count} version(s)", Severity.Success);

                // Navigate to comparison page with the execution name
                NavigationManager.NavigateTo(
                    $"/admin/test-runs/compare?executions={Uri.EscapeDataString(executionName)}");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Snackbar.Add($"Test run failed: {error}", Severity.Error);
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
