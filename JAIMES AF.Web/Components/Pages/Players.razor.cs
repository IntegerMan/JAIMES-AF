using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class Players
{
    private PlayerInfoResponse[]? players;
    private bool isLoading = true;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlayersAsync();
    }

    private async Task LoadPlayersAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            PlayerListResponse? resp = await Http.GetFromJsonAsync<PlayerListResponse>("/players");
            players = resp?.Players ?? [];
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("Players").LogError(ex, "Failed to load players from API");
            errorMessage = "Failed to load players: " + ex.Message;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}

