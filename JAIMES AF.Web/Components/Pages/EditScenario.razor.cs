using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditScenario
{
    [Parameter]
    public string ScenarioId { get; set; } = string.Empty;

    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] rulesets = [];
    private string? selectedRulesetId;
    private string name = string.Empty;
    private string? description;
    private string systemPrompt = string.Empty;
    private string newGameInstructions = string.Empty;
    private bool isLoading = true;
    private bool isSaving = false;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            var rulesetsTask = Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            var scenarioTask = Http.GetFromJsonAsync<ScenarioResponse>($"/scenarios/{ScenarioId}");

            await Task.WhenAll(rulesetsTask, scenarioTask);

            RulesetListResponse? rulesetsResponse = await rulesetsTask;
            ScenarioResponse? scenarioResponse = await scenarioTask;

            if (scenarioResponse == null)
            {
                errorMessage = $"Scenario with ID '{ScenarioId}' not found.";
                isLoading = false;
                StateHasChanged();
                return;
            }

            rulesets = rulesetsResponse?.Rulesets ?? [];
            selectedRulesetId = scenarioResponse.RulesetId;
            name = scenarioResponse.Name;
            description = scenarioResponse.Description;
            systemPrompt = scenarioResponse.SystemPrompt;
            newGameInstructions = scenarioResponse.NewGameInstructions;
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, "Failed to load scenario or rulesets from API");
            errorMessage = "Failed to load scenario: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(name) &&
               !string.IsNullOrWhiteSpace(systemPrompt) &&
               !string.IsNullOrWhiteSpace(newGameInstructions);
    }

    private async Task UpdateScenarioAsync()
    {
        if (!IsFormValid())
        {
            errorMessage = "Please fill in all required fields.";
            StateHasChanged();
            return;
        }

        isSaving = true;
        errorMessage = null;
        try
        {
            UpdateScenarioRequest request = new()
            {
                RulesetId = selectedRulesetId!,
                Description = description,
                Name = name,
                SystemPrompt = systemPrompt,
                NewGameInstructions = newGameInstructions
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

                errorMessage = $"Failed to update scenario: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditScenario").LogError(ex, "Failed to update scenario");
            errorMessage = "Failed to update scenario: " + ex.Message;
            StateHasChanged();
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/scenarios");
    }
}

