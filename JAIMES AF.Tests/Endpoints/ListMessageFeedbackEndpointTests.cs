using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Tests.Endpoints;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListMessageFeedbackEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task ListMessageFeedback_ReturnsFeedback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // 1. Create a Game
        var createRequest = new { ScenarioId = "test-scenario", PlayerId = "test-player" };
        var createGameResponse = await Client.PostAsJsonAsync("/games/", createRequest, ct);
        createGameResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Retrieve the game via List endpoint which correctly loads relations
        var listGamesResponse = await Client.GetAsync("/games/", ct);
        var gamesList = await listGamesResponse.Content
            .ReadFromJsonAsync<MattEland.Jaimes.ServiceDefinitions.Responses.ListGamesResponse>(ct);
        gamesList.ShouldNotBeNull();
        var game = gamesList.Games.First();
        game.ShouldNotBeNull();

        // 2. Get the initial greeting message ID (this is an assistant message suitable for feedback)
        // Accessing the game details to find the message
        var gameResponse = await Client.GetAsync($"/games/{game.GameId}", ct);
        var fetchedGame = await gameResponse.Content.ReadFromJsonAsync<GameStateResponse>(ct);
        fetchedGame.ShouldNotBeNull();
        fetchedGame.Messages.ShouldNotBeNull();
        var message = fetchedGame.Messages.FirstOrDefault(m => m.PlayerId == null);
        message.ShouldNotBeNull();
        int messageId = message.Id;

        // 3. Submit Feedback
        var feedbackRequest = new SubmitMessageFeedbackRequest
        {
            IsPositive = false,
            Comment = "This is a test feedback"
        };
        var feedbackResponse = await Client.PostAsJsonAsync($"/messages/{messageId}/feedback", feedbackRequest, ct);
        feedbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 4. List Feedback
        var listResponse = await Client.GetAsync("/admin/feedback", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feedbackList = await listResponse.Content.ReadFromJsonAsync<FeedbackListResponse>(ct);

        // 5. Verify
        feedbackList.ShouldNotBeNull();
        feedbackList.Items.ShouldNotBeEmpty();
        var item = feedbackList.Items.First();
        item.MessageId.ShouldBe(messageId);
        item.Comment.ShouldBe("This is a test feedback");
        item.IsPositive.ShouldBeFalse();
        item.GameId.ShouldBe(game.GameId);
        item.GamePlayerName.ShouldNotBeNullOrEmpty();
        item.GameScenarioName.ShouldNotBeNullOrEmpty();
        item.GameRulesetId.ShouldBe("test-ruleset");
    }
}
