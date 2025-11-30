using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RulesSearchTest
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public ILoggerFactory LoggerFactory { get; set; } = null!;

    private RulesetInfoResponse[] rulesets = [];
    private string searchQuery = string.Empty;
    private string? selectedRulesetId;
    private bool isSearching = false;
    private string? errorMessage;
    private SearchRulesResponse? searchResult;

    protected override async Task OnInitializedAsync()
    {
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        try
        {
            RulesetListResponse? response = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            rulesets = response?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RulesSearchTest").LogError(ex, "Failed to load rulesets from API");
            errorMessage = "Failed to load rulesets: " + ex.Message;
        }
    }

    private async Task HandleSearchQueryKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !isSearching && !string.IsNullOrWhiteSpace(searchQuery))
        {
            await SearchRulesAsync();
        }
    }

    private async Task SearchRulesAsync()
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            errorMessage = "Please enter a search query.";
            return;
        }

        isSearching = true;
        errorMessage = null;
        searchResult = null;

        try
        {
            SearchRulesRequest request = new()
            {
                Query = searchQuery,
                RulesetId = selectedRulesetId
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/rules/search", request);
            
            if (response.IsSuccessStatusCode)
            {
                searchResult = await response.Content.ReadFromJsonAsync<SearchRulesResponse>();
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                errorMessage = $"Search failed: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RulesSearchTest").LogError(ex, "Failed to search rules");
            errorMessage = "Failed to search rules: " + ex.Message;
        }
        finally
        {
            isSearching = false;
            StateHasChanged();
        }
    }
}

