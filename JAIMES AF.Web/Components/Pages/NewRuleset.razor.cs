namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewRuleset
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private string _rulesetId = string.Empty;
    private string _name = string.Empty;
    private bool _isSaving = false;
    private string? _errorMessage;

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_rulesetId) &&
               !string.IsNullOrWhiteSpace(_name);
    }

    private async Task CreateRulesetAsync()
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
            CreateRulesetRequest request = new()
            {
                Id = _rulesetId,
                Name = _name
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/rulesets", request);

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
                    $"Failed to create ruleset: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewRuleset").LogError(ex, "Failed to create ruleset");
            _errorMessage = "Failed to create ruleset: " + ex.Message;
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