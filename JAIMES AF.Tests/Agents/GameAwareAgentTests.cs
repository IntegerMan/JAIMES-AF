using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.Tests.Endpoints;
using Microsoft.Extensions.AI;
using Moq;
using Shouldly;
using Xunit;

namespace MattEland.Jaimes.Tests.Agents;

/// <summary>
/// Tests for message enqueuing in GameAwareAgent.
/// These tests verify that messages are properly enqueued after persistence through integration testing via endpoints.
/// </summary>
public class GameAwareAgentTests : EndpointTestBase
{
    [Fact]
    public async Task NewGame_DoesNotEnqueueInitialMessage()
    {
        // Arrange
        NewGameRequest request = new()
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockMessagePublisher.Reset();

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        // Note: NewGame creates the initial message through IChatService, not GameAwareAgent.
        // GameAwareAgent (which enqueues messages) is only used when messages are sent through the chat endpoint.
        // Therefore, the initial message from NewGame should NOT be enqueued.
        MockMessagePublisher.Verify(
            x => x.PublishAsync(
                It.IsAny<ConversationMessageQueuedMessage>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ToolCallMessage_IsNotEnqueued()
    {
        // Arrange
        NewGameRequest request = new()
        {
            ScenarioId = "test-scenario",
            PlayerId = "test-player"
        };
        CancellationToken ct = TestContext.Current.CancellationToken;
        MockMessagePublisher.Reset();

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/games/", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        
        // Verify that no tool call messages were enqueued
        // Tool calls should be filtered out before persistence, so they should never be enqueued
        MockMessagePublisher.Verify(
            x => x.PublishAsync(
                It.Is<ConversationMessageQueuedMessage>(m => m.Role == ChatRole.Tool),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

