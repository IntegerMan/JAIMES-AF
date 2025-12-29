namespace MattEland.Jaimes.Web.Components.Pages;

public partial class RulesSearchTest
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string _searchQuery = string.Empty;
    private string? _selectedRulesetId;
    private bool _storeResults = true;
    private bool _isSearching = false;
    private string? _errorMessage;
    private SearchRulesResponse? _searchResult;
    private string? _toolOutput;

    protected override async Task OnInitializedAsync()
    {
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        try
        {
            RulesetListResponse? response = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            _rulesets = response?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RulesSearchTest").LogError(ex, "Failed to load rulesets from API");
            _errorMessage = "Failed to load rulesets: " + ex.Message;
        }
    }

    private async Task HandleSearchQueryKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !_isSearching && !string.IsNullOrWhiteSpace(_searchQuery)) await SearchRulesAsync();
    }

    private async Task SearchRulesAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _errorMessage = "Please enter a search query.";
            return;
        }

        _isSearching = true;
        _errorMessage = null;
        _searchResult = null;

        try
        {
            SearchRulesRequest request = new()
            {
                Query = _searchQuery,
                RulesetId = _selectedRulesetId,
                StoreResults = _storeResults
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/rules/search", request);

            if (response.IsSuccessStatusCode)
            {
                _searchResult = await response.Content.ReadFromJsonAsync<SearchRulesResponse>();
                
                // Format the output as the tool would return it
                if (_searchResult != null && _searchResult.Results.Length > 0)
                {
                    List<string> resultTexts = _searchResult.Results.Select(r => r.Text).ToList();
                    _toolOutput = string.Join("\n\n---\n\n", resultTexts);
                }
                else
                {
                    _toolOutput = "No relevant rules found for your query.";
                }
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                _errorMessage = $"Search failed: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("RulesSearchTest").LogError(ex, "Failed to search rules");
            _errorMessage = "Failed to search rules: " + ex.Message;
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }
}