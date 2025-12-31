using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class NewAgent
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    [Inject] public NavigationManager Navigation { get; set; } = null!;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override void OnInitialized()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Admin", href: "/admin"),
            new BreadcrumbItem("Agents", href: "/agents"),
            new BreadcrumbItem("New Agent", href: null, disabled: true)
        };
    }

    private string _name = string.Empty;
    private string _role = string.Empty;
    private string _instructions = string.Empty;
    private bool _isSaving = false;
    private string? _errorMessage;

    private bool IsFormValid()
    {
        return !string.IsNullOrWhiteSpace(_name) &&
               !string.IsNullOrWhiteSpace(_role) &&
               !string.IsNullOrWhiteSpace(_instructions);
    }

    private async Task CreateAgentAsync()
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
            CreateAgentRequest request = new()
            {
                Name = _name,
                Role = _role,
                Instructions = _instructions
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/agents", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/agents");
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
                    $"Failed to create agent: {response.ReasonPhrase}{(string.IsNullOrEmpty(body) ? string.Empty : " - " + body)}";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("NewAgent").LogError(ex, "Failed to create agent");
            _errorMessage = "Failed to create agent: " + ex.Message;
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
        Navigation.NavigateTo("/agents");
    }
}
