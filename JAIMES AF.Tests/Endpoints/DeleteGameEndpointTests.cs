namespace MattEland.Jaimes.Tests.Endpoints;

public class DeleteGameEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task DeleteGameEndpoint_DeletesGameSuccessfully()
    {
        // Arrange - Create a game first
        var createRequest = new { ScenarioId = "test-scenario", PlayerId = "test-player" };
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/games/", createRequest, ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        NewGameResponse? newGame = await createResponse.Content.ReadFromJsonAsync<NewGameResponse>(ct);
        newGame.ShouldNotBeNull();
        Guid gameId = newGame.GameId;

        // Act - Delete the game
        HttpResponseMessage deleteResponse = await Client.DeleteAsync($"/games/{gameId}", ct);

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify game is actually deleted
        HttpResponseMessage getResponse = await Client.GetAsync($"/games/{gameId}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGameEndpoint_ReturnsNotFoundWhenGameDoesNotExist()
    {
        // Arrange
        Guid nonExistentGameId = Guid.NewGuid();
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act - Try to delete a non-existent game
        HttpResponseMessage response = await Client.DeleteAsync($"/games/{nonExistentGameId}", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}