namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Players
{
    private PlayerInfoResponse[]? _players;
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlayersAsync();
    }

    private async Task LoadPlayersAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            PlayerListResponse? resp = await Http.GetFromJsonAsync<PlayerListResponse>("/players");
            _players = resp?.Players ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Players").LogError(ex, "Failed to load players from API");
            _errorMessage = "Failed to load players: " + ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}