namespace MattEland.Jaimes.Web.Components.Pages;

public partial class EditRuleset
{
    [Parameter] public string RulesetId { get; set; } = string.Empty;

    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private string _name = string.Empty;
    private bool _isLoading = true;
    private bool _isSaving = false;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Rulesets", href: "/rulesets"),
            new BreadcrumbItem("Edit Ruleset", href: null, disabled: true)
        };
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            RulesetResponse? rulesetResponse = await Http.GetFromJsonAsync<RulesetResponse>($"/rulesets/{RulesetId}");

            if (rulesetResponse == null)
            {
                _errorMessage = $"Ruleset with ID '{RulesetId}' not found.";
                _isLoading = false;
                StateHasChanged();
                return;
            }

            _name = rulesetResponse.Name;

            _breadcrumbs = new List<BreadcrumbItem>
            {
                new BreadcrumbItem("Home", href: "/"),
                new BreadcrumbItem("Rulesets", href: "/rulesets"),
                new BreadcrumbItem(rulesetResponse.Name, href: null, disabled: true),
                new BreadcrumbItem("Edit", href: null, disabled: true)
            };
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditRuleset").LogError(ex, "Failed to load ruleset from API");
            _errorMessage = "Failed to load ruleset: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_name);
    }

    private async Task UpdateRulesetAsync()
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
            UpdateRulesetRequest request = new()
            {
                Name = _name
            };

            HttpResponseMessage response = await Http.PutAsJsonAsync($"/rulesets/{RulesetId}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/rulesets");
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
                    $"Failed to update ruleset: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("EditRuleset").LogError(ex, "Failed to update ruleset");
            _errorMessage = "Failed to update ruleset: " + ex.Message;
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
        Navigation.NavigateTo("/rulesets");
    }
}