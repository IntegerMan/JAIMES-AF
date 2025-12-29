using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class PlayerSentimentTest
{
	[Inject] public HttpClient Http { get; set; } = null!;

	[Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

	private GameInfoResponse[] _games = [];
	private Guid? _selectedGameId;
	private bool _isLoading = false;
	private string? _errorMessage;
	private string? _toolOutput;

	protected override async Task OnInitializedAsync()
	{
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
			LoggerFactory.CreateLogger("PlayerSentimentTest").LogError(ex, "Failed to load games from API");
			_errorMessage = "Failed to load games: " + ex.Message;
		}
	}

	private async Task GetSentimentAsync()
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
			// Get the game state which includes messages
			GameStateResponse? gameState = await Http.GetFromJsonAsync<GameStateResponse>($"/games/{_selectedGameId.Value}");

			if (gameState == null)
			{
				_errorMessage = "Game not found.";
				return;
			}

			// Filter messages with sentiment for the current game and player
			// Order by CreatedAt descending and take the last 5
			MessageResponse[] messagesWithSentiment = gameState.Messages
				.Where(m => m.Sentiment != null)
				.OrderByDescending(m => m.CreatedAt)
				.Take(5)
				.ToArray();

			if (messagesWithSentiment.Length == 0)
			{
				_toolOutput = "No sentiment analysis results available for this player in the current game.";
				return;
			}

			// Calculate average sentiment
			double averageSentiment = messagesWithSentiment.Average(m => m.Sentiment!.Value);
			string averageLabel = averageSentiment switch
			{
				> 0.33 => "Positive",
				< -0.33 => "Negative",
				_ => "Neutral"
			};

			// Format results as the tool would
			List<string> resultTexts = new();
			
			// Add average sentiment at the beginning
			resultTexts.Add($"Average Sentiment: {averageLabel} ({averageSentiment:F2})");
			resultTexts.Add(string.Empty); // Empty line separator
			
			foreach (MessageResponse message in messagesWithSentiment)
			{
				string sentimentLabel = message.Sentiment switch
				{
					1 => "Positive",
					-1 => "Negative",
					0 => "Neutral",
					_ => "Unknown"
				};

				string messagePreview = message.Text.Length > 100
					? message.Text.Substring(0, 100) + "..."
					: message.Text;

				resultTexts.Add(
					$"[{message.CreatedAt:yyyy-MM-dd HH:mm:ss}] {sentimentLabel} ({message.Sentiment}): {messagePreview}");
			}

			_toolOutput = string.Join("\n", resultTexts);
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger("PlayerSentimentTest").LogError(ex, "Failed to get sentiment");
			_errorMessage = "Failed to get sentiment: " + ex.Message;
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

