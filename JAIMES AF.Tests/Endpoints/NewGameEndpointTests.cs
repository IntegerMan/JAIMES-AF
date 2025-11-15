using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class NewGameEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task NewGameEndpoint_CreatesGame_ReturnsCreated()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        NewGameResponse? game = await response.Content.ReadFromJsonAsync<NewGameResponse>(cancellationToken: ct);
        game.ShouldNotBeNull();
        game.GameId.ShouldNotBe(Guid.Empty);
        game.Messages.ShouldNotBeNull();
        game.Messages.ShouldHaveSingleItem();
        game.Messages[0].Text.ShouldNotBeNull();
        game.Messages[0].Text.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task NewGameEndpoint_WithMissingFields_ReturnsBadRequest()
    {
        // Arrange
        var request = new { }; // Empty request
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NewGameEndpoint_WithNonexistentPlayer_ReturnsBadRequest()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "test-scenario",
            PlayerId = "nonexistent-player"
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NewGameEndpoint_WithNonexistentScenario_ReturnsBadRequest()
    {
        // Arrange
        NewGameRequest request = new NewGameRequest
        {
            ScenarioId = "nonexistent-scenario",
            PlayerId = "test-player"
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
