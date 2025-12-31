using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class PlayerInfoTest
{
	[Inject] public HttpClient Http { get; set; } = null!;

	[Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

	private GameInfoResponse[] _games = [];
	private Guid? _selectedGameId;
	private bool _isLoading = false;
	private string? _errorMessage;
	private string? _toolOutput;
	private List<BreadcrumbItem> _breadcrumbs = new();

	protected override async Task OnInitializedAsync()
	{
		_breadcrumbs = new List<BreadcrumbItem>
		{
			new BreadcrumbItem("Home", href: "/"),
			new BreadcrumbItem("Tools", href: null, disabled: true),
			new BreadcrumbItem("Player Info", href: null, disabled: true)
		};

		await LoadGamesAsync();
	}

	private async Task LoadGamesAsync()
	{
		try
		{
			ListGamesResponse? response = await Http.GetFromJsonAsync<ListGamesResponse>("/games");
			_games = response?.Games ?? [];
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("PlayerInfoTest").LogError(ex, "Failed to load games from API");
			_errorMessage = "Failed to load games: " + ex.Message;
		}
	}

	private async Task GetPlayerInfoAsync()
	{
		if (!_selectedGameId.HasValue)
		{
			_errorMessage = "Please select a game.";
			return;
		}

		_isLoading = true;
		_errorMessage = null;
		_toolOutput = null;

		try
		{
			// Get the game state which includes player information
			GameStateResponse? gameState = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{_selectedGameId.Value}");

			if (gameState == null)
			{
				_errorMessage = "Game not found.";
				return;
			}

			// Get player details to include description
			PlayerResponse? playerResponse = null;
			try
			{
				playerResponse = await Http.GetFromJsonAsync<PlayerResponse>($"/players/{gameState.PlayerId}");
			}
			catch
			{
				// Player endpoint might fail, but we can still show basic info
			}

			// Format as the tool would
			string info = $"Player Name: {gameState.PlayerName}\nPlayer ID: {gameState.PlayerId}";

			if (playerResponse != null && !string.IsNullOrWhiteSpace(playerResponse.Description))
			{
				info += $"\nPlayer Description: {playerResponse.Description}";
			}

			_toolOutput = info;
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("PlayerInfoTest").LogError(ex, "Failed to get player info");
			_errorMessage = "Failed to get player info: " + ex.Message;
		}
		finally
		{
			_isLoading = false;
			StateHasChanged();
		}
	}

	private string FormatGameDisplay(GameInfoResponse game)
	{
		return $"{game.PlayerName} - {game.ScenarioName} ({game.RulesetName})";
	}
}

