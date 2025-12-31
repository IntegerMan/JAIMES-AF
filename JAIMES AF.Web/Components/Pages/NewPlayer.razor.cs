namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewPlayer
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private RulesetInfoResponse[] _rulesets = [];
    private string _playerId = string.Empty;
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
            new BreadcrumbItem("New Player", href: null, disabled: true)
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
            LoggerFactory.CreateLogger("NewPlayer").LogError(ex, "Failed to load rulesets from API");
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
        return !string.IsNullOrWhiteSpace(_playerId) &&
               !string.IsNullOrWhiteSpace(_selectedRulesetId) &&
               !string.IsNullOrWhiteSpace(_name);
    }

    private async Task CreatePlayerAsync()
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
            CreatePlayerRequest request = new()
            {
                Id = _playerId,
                RulesetId = _selectedRulesetId!,
                Description = _description,
                Name = _name
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/players", request);

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
                    $"Failed to create player: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewPlayer").LogError(ex, "Failed to create player");
            _errorMessage = "Failed to create player: " + ex.Message;
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