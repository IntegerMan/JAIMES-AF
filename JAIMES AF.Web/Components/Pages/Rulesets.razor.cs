namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Rulesets
{
    private RulesetInfoResponse[]? _rulesets;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Rulesets", href: null, disabled: true)
        };
        await LoadRulesetsAsync();
    }

    private async Task LoadRulesetsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            RulesetListResponse? resp = await Http.GetFromJsonAsync<RulesetListResponse>("/rulesets");
            _rulesets = resp?.Rulesets ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Rulesets").LogError(ex, "Failed to load rulesets from API");
            _errorMessage = "Failed to load rulesets: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}