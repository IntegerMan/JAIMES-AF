namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Games
{
    private GameInfoResponse[]? _games;
    private bool _isLoading = true;
    private string? _errorMessage;

    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync()
    {
        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/"),
            new BreadcrumbItem("Games", href: null, disabled: true)
        };
        await LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            ListGamesResponse? resp = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
            _games = resp?.Games ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to load games from API");
            _errorMessage = "Failed to load games: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteGameAsync(Guid gameId)
    {
        bool? result = await DialogService.ShowMessageBox(
            "Delete Game",
            "Are you sure you want to delete this game? This action cannot be undone.",
            "Delete",
            cancelText: "Cancel");

        if (result == true)
            try
            {
                HttpResponseMessage response = await Http.DeleteAsync($"/games/{gameId}");
                if (response.IsSuccessStatusCode)
                {
                    await LoadGamesAsync();
                }
                else
                {
                    LoggerFactory.CreateLogger("Games")
                        .LogError("Failed to delete game: {StatusCode}", response.StatusCode);
                    _errorMessage = "Failed to delete game. Please try again.";
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger("Games").LogError(ex, "Failed to delete game from API");
                _errorMessage = "Failed to delete game: " + ex.Message;
                StateHasChanged();
            }
    }

    /// <summary>
    /// Converts a UTC DateTime to local time for display.
    /// </summary>
    private DateTime ToLocalTime(DateTime utcTime)
    {
        // Ensure the DateTime is treated as UTC, then convert to local time
        if (utcTime.Kind == DateTimeKind.Unspecified)
        {
            utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
        }

        return utcTime.ToLocalTime();
    }
}