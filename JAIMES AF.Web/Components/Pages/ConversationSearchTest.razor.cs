using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Web.Components.Pages;

public partial class ConversationSearchTest
{
    [Inject] public HttpClient Http { get; set; } = null!;

    [Inject] public ILoggerFactory LoggerFactory { get; set; } = null!;

    private GameInfoResponse[] _games = [];
    private Guid? _selectedGameId;
    private string _searchQuery = string.Empty;
    private int _limit = 5;
    private bool _isSearching = false;
    private string? _errorMessage;
    private ConversationSearchResponse? _searchResult;
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
            LoggerFactory.CreateLogger("ConversationSearchTest").LogError(ex, "Failed to load games from API");
            _errorMessage = "Failed to load games: " + ex.Message;
        }
    }

    private async Task HandleSearchQueryKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !_isSearching && !string.IsNullOrWhiteSpace(_searchQuery) && _selectedGameId.HasValue)
        {
            await SearchConversationsAsync();
        }
    }

    private async Task SearchConversationsAsync()
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _errorMessage = "Please enter a search query.";
            return;
        }

        if (!_selectedGameId.HasValue)
        {
            _errorMessage = "Please select a game.";
            return;
        }

        _isSearching = true;
        _errorMessage = null;
        _searchResult = null;

        try
        {
            ConversationSearchRequest request = new()
            {
                GameId = _selectedGameId.Value,
                Query = _searchQuery,
                Limit = _limit
            };

            HttpResponseMessage response = await Http.PostAsJsonAsync("/conversations/search", request);

            if (response.IsSuccessStatusCode)
            {
                _searchResult = await response.Content.ReadFromJsonAsync<ConversationSearchResponse>();
                
                // Format the output as the tool would return it
                if (_searchResult != null && _searchResult.Results.Length > 0)
                {
                    List<string> resultTexts = new();
                    foreach (ConversationSearchResult result in _searchResult.Results)
                    {
                        List<string> messageParts = new();

                        // Add previous message if available
                        if (result.PreviousMessage != null)
                        {
                            messageParts.Add($"[Previous] {result.PreviousMessage.ParticipantName}: {result.PreviousMessage.Text}");
                        }

                        // Add matched message
                        messageParts.Add($"[Matched - Relevancy: {result.Relevancy:F2}] {result.MatchedMessage.ParticipantName}: {result.MatchedMessage.Text}");

                        // Add next message if available
                        if (result.NextMessage != null)
                        {
                            messageParts.Add($"[Next] {result.NextMessage.ParticipantName}: {result.NextMessage.Text}");
                        }

                        resultTexts.Add(string.Join("\n", messageParts));
                    }

                    _toolOutput = string.Join("\n\n---\n\n", resultTexts);
                }
                else
                {
                    _toolOutput = "No relevant conversation history found for your query.";
                }
            }
            else
            {
                string errorText = await response.Content.ReadAsStringAsync();
                _errorMessage = $"Search failed: {response.StatusCode} - {errorText}";
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.CreateLogger("ConversationSearchTest").LogError(ex, "Failed to search conversations");
            _errorMessage = "Failed to search conversations: " + ex.Message;
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }

    private string FormatGameDisplay(GameInfoResponse game)
    {
        return $"{game.PlayerName} - {game.ScenarioName} ({game.RulesetName})";
    }
}

