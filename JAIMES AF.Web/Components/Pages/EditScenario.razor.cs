namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditScenario
{
    [Parameter] public string ScenarioId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string? _selectedRulesetId;
    private string _name = string.Empty;
    private string? _description;
    private string _systemPrompt = string.Empty;
    private string? _initialGreeting;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;

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

            await Task.WhenAll(rulesetsTask, scenarioTask);

            RulesetListResponse? rulesetsResponse = await rulesetsTask;
            ScenarioResponse? scenarioResponse = await scenarioTask;

            if (scenarioResponse == null)
            {
                _errorMessage = $"Scenario with ID '{ScenarioId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _rulesets = rulesetsResponse?.Rulesets ?? [];
            _selectedRulesetId = scenarioResponse.RulesetId;
            _name = scenarioResponse.Name;
            _description = scenarioResponse.Description;
            _systemPrompt = scenarioResponse.SystemPrompt;
            _initialGreeting = scenarioResponse.InitialGreeting;
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

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(_name) &&
               !string.IsNullOrWhiteSpace(_systemPrompt);
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
                SystemPrompt = _systemPrompt,
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

    private void Cancel()
    {
        Navigation.NavigateTo("/scenarios");
    }
}