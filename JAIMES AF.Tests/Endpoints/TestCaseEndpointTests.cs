using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Tests.Endpoints;

public class TestCaseEndpointTests : EndpointTestBase
{
    private Guid _gameId;
    private int _messageId;

    protected override async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        await base.SeedTestDataAsync(context, cancellationToken);

        // Create a game and player message for test case tests
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Test Case Game",
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            CreatedAt = DateTime.UtcNow
        };
        context.Games.Add(game);
        _gameId = game.Id;

        var message = new Message
        {
            GameId = game.Id,
            Text = "What can I do here?",
            PlayerId = "test-player",
            AgentId = "test-agent",
            InstructionVersionId = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
        _messageId = message.Id;
    }

    [Fact]
    public async Task ListTestCasesEndpoint_ReturnsEmptyList_WhenNoTestCases()
    {
        // Act
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/test-cases", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<TestCaseResponse>>(ct);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateTestCaseEndpoint_CreatesTestCase()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        var request = new { MessageId = _messageId, Name = "Test Case 1", Description = "Description" };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/test-cases", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TestCaseResponse>(ct);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test Case 1");
        result.Description.ShouldBe("Description");
        result.MessageId.ShouldBe(_messageId);
        result.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTestCaseEndpoint_Returns400_WhenMessageNotFound()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        var request = new { MessageId = 300099, Name = "Test", Description = (string?)null };

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/test-cases", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTestCaseEndpoint_ReturnsTestCase_WhenExists()
    {
        // Arrange - Create a test case first
        CancellationToken ct = TestContext.Current.CancellationToken;
        var createRequest = new { MessageId = _messageId, Name = "Get Test", Description = (string?)null };
        var createResponse = await Client.PostAsJsonAsync("/test-cases", createRequest, ct);
        var created = await createResponse.Content.ReadFromJsonAsync<TestCaseResponse>(ct);

        // Act
        HttpResponseMessage response = await Client.GetAsync($"/test-cases/{created!.Id}", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestCaseResponse>(ct);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
        result.Name.ShouldBe("Get Test");
    }

    [Fact]
    public async Task GetTestCaseEndpoint_Returns404_WhenNotExists()
    {
        // Act
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.GetAsync("/test-cases/300099", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTestCaseEndpoint_UpdatesNameAndDescription()
    {
        // Arrange - Create a test case first
        CancellationToken ct = TestContext.Current.CancellationToken;
        var createRequest = new { MessageId = _messageId, Name = "Original", Description = (string?)null };
        var createResponse = await Client.PostAsJsonAsync("/test-cases", createRequest, ct);
        var created = await createResponse.Content.ReadFromJsonAsync<TestCaseResponse>(ct);

        var updateRequest = new { Name = "Updated Name", Description = "Updated Description" };

        // Act
        HttpResponseMessage response = await Client.PutAsJsonAsync($"/test-cases/{created!.Id}", updateRequest, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TestCaseResponse>(ct);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated Description");
    }

    [Fact]
    public async Task DeleteTestCaseEndpoint_DeactivatesTestCase()
    {
        // Arrange - Create a test case first
        CancellationToken ct = TestContext.Current.CancellationToken;
        var createRequest = new { MessageId = _messageId, Name = "Delete Test", Description = (string?)null };
        var createResponse = await Client.PostAsJsonAsync("/test-cases", createRequest, ct);
        var created = await createResponse.Content.ReadFromJsonAsync<TestCaseResponse>(ct);

        // Act
        HttpResponseMessage response = await Client.DeleteAsync($"/test-cases/{created!.Id}", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify it's no longer in the active list
        var listResponse = await Client.GetAsync("/test-cases", ct);
        var list = await listResponse.Content.ReadFromJsonAsync<List<TestCaseResponse>>(ct);
        list.ShouldNotBeNull();
        list.ShouldNotContain(tc => tc.Id == created.Id);
    }

    [Fact]
    public async Task DeleteTestCaseEndpoint_Returns404_WhenNotExists()
    {
        // Act
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.DeleteAsync("/test-cases/300099", ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
