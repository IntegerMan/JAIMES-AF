namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewScenario
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string _scenarioId = string.Empty;
    private string? _selectedRulesetId;
    private string _name = string.Empty;
    private string? _description;
    private string? _initialGreeting;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Content", href: null, disabled: true),
            new BreadcrumbItem("Scenarios", href: "/scenarios"),
            new BreadcrumbItem("New Scenario", href: null, disabled: true)
        };
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            RulesetListResponse? response = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            _rulesets = response?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewScenario").LogError(ex, "Failed to load rulesets from API");
            _errorMessage = "Failed to load rulesets: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_scenarioId) &&
               !string.IsNullOrWhiteSpace(_selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(_name);
    }

    private async Task CreateScenarioAsync()
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
            CreateScenarioRequest request = new()
            {
                Id = _scenarioId,
                RulesetId = _selectedRulesetId!,
                Description = _description,
                Name = _name,
                InitialGreeting = _initialGreeting
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/scenarios", request);

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
                    $"Failed to create scenario: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewScenario").LogError(ex, "Failed to create scenario");
            _errorMessage = "Failed to create scenario: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/scenarios");
    }
}