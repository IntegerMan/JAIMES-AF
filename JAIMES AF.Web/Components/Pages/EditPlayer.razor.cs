namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditPlayer
{
    [Parameter] public string PlayerId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string? _selectedRulesetId;
    private string _name = string.Empty;
    private string? _description;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Players", href: "/players"),
            new BreadcrumbItem("Edit Player", href: null, disabled: true)
        };
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            Task<RulesetListResponse?> rulesetsTask = Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            Task<PlayerResponse?> playerTask = Http.GetFromJsonAsync<PlayerResponse>($"/players/{PlayerId}");

            await Task.WhenAll(rulesetsTask, playerTask);

            RulesetListResponse? rulesetsResponse = await rulesetsTask;
            PlayerResponse? playerResponse = await playerTask;

            if (playerResponse == null)
            {
                _errorMessage = $"Player with ID '{PlayerId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _rulesets = rulesetsResponse?.Rulesets ?? [];
            _selectedRulesetId = playerResponse.RulesetId;
            _name = playerResponse.Name;
            _description = playerResponse.Description;

            _breadcrumbs = new List<BreadcrumbItem>
            {
                new BreadcrumbItem("Home", href: "/"),
                new BreadcrumbItem("Players", href: "/players"),
                new BreadcrumbItem(playerResponse.Name, href: null, disabled: true),
                new BreadcrumbItem("Edit", href: null, disabled: true)
            };
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditPlayer").LogError(ex, "Failed to load player or rulesets from API");
            _errorMessage = "Failed to load player: " + ex.Message;
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
               !string.IsNullOrWhiteSpace(_name);
    }

    private async Task UpdatePlayerAsync()
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
            UpdatePlayerRequest request = new()
            {
                RulesetId = _selectedRulesetId!,
                Description = _description,
                Name = _name
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/players/{PlayerId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/players");
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
                    $"Failed to update player: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditPlayer").LogError(ex, "Failed to update player");
            _errorMessage = "Failed to update player: " + ex.Message;
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
        Navigation.NavigateTo("/players");
    }
}