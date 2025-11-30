using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewScenario
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] rulesets = [];
    private string scenarioId = string.Empty;
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
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            RulesetListResponse? response = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            rulesets = response?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewScenario").LogError(ex, "Failed to load rulesets from API");
            errorMessage = "Failed to load rulesets: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(scenarioId) &&
               !string.IsNullOrWhiteSpace(selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(name) &&
               !string.IsNullOrWhiteSpace(systemPrompt) &&
               !string.IsNullOrWhiteSpace(newGameInstructions);
    }

    private async Task CreateScenarioAsync()
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
            CreateScenarioRequest request = new()
            {
                Id = scenarioId,
                RulesetId = selectedRulesetId!,
                Description = description,
                Name = name,
                SystemPrompt = systemPrompt,
                NewGameInstructions = newGameInstructions
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

                errorMessage = $"Failed to create scenario: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewScenario").LogError(ex, "Failed to create scenario");
            errorMessage = "Failed to create scenario: " + ex.Message;
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

