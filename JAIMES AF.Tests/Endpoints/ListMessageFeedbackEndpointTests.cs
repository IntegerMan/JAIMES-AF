using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Tests.Endpoints;

namespace MattEland.Jaimes.Tests.Endpoints;

public class ListMessageFeedbackEndpointTests : EndpointTestBase
{
    private int _nonScriptedMessageId;

    protected override async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        await base.SeedTestDataAsync(context, cancellationToken);

        // Create a game with a non-scripted AI message for feedback testing
        var game = new Game
        {
            Id = Guid.NewGuid(),
            RulesetId = "test-ruleset",
            ScenarioId = "test-scenario",
            PlayerId = "test-player",
            Title = "Feedback Test Game",
            CreatedAt = DateTime.UtcNow,
            AgentId = "test-agent",
            InstructionVersionId = 1
        };
        context.Games.Add(game);

        // Create a non-scripted AI message (this would normally come from actual AI response)
        var aiMessage = new Message
        {
            GameId = game.Id,
            Text = "This is a non-scripted AI response for testing.",
            PlayerId = null, // AI message
            CreatedAt = DateTime.UtcNow,
            IsScriptedMessage = false,
            AgentId = "test-agent",
            InstructionVersionId = 1
        };
        context.Messages.Add(aiMessage);
        await context.SaveChangesAsync(cancellationToken);

        _nonScriptedMessageId = aiMessage.Id;
    }

    [Fact]
    public async Task ListMessageFeedback_ReturnsFeedback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // The non-scripted message was created in SeedTestDataAsync
        int messageId = _nonScriptedMessageId;
        messageId.ShouldBeGreaterThan(0);

        // 1. Submit Feedback on the non-scripted message
        var feedbackRequest = new SubmitMessageFeedbackRequest
        {
            IsPositive = false,
            Comment = "This is a test feedback"
        };
        var feedbackResponse = await Client.PostAsJsonAsync($"/messages/{messageId}/feedback", feedbackRequest, ct);
        feedbackResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 2. List Feedback
        var listResponse = await Client.GetAsync("/admin/feedback", ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var feedbackList = await listResponse.Content.ReadFromJsonAsync<FeedbackListResponse>(ct);

        // 3. Verify - feedback on non-scripted messages should be included
        feedbackList.ShouldNotBeNull();
        feedbackList.Items.ShouldNotBeEmpty();
        var item = feedbackList.Items.First();
        item.MessageId.ShouldBe(messageId);
        item.Comment.ShouldBe("This is a test feedback");
        item.IsPositive.ShouldBeFalse();
    }
}
