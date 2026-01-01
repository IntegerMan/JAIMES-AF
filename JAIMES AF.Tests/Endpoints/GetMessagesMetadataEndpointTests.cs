using System.Net.Http.Json;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class GetMessagesMetadataEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GetMessagesMetadata_ReportsMissingEvaluators_WhenNotAllEvaluatorsRun()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        int messageId;

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();

            // Add two evaluators
            var eval1 = new Evaluator { Name = "Evaluator 1" };
            var eval2 = new Evaluator { Name = "Evaluator 2" };
            await context.Evaluators.AddRangeAsync([eval1, eval2], ct);

            // Add one message
            var msg = new Message
            {
                Text = "Test message",
                PlayerId = null,
                IsScriptedMessage = false,
                GameId = Guid.NewGuid(),
                AgentId = "test-agent",
                InstructionVersionId = 1
            };
            await context.Messages.AddAsync(msg, ct);
            await context.SaveChangesAsync(ct);
            messageId = msg.Id;

            // Add only ONE metric (one evaluator run, one missing)
            await context.MessageEvaluationMetrics.AddAsync(new MessageEvaluationMetric
            {
                MessageId = msg.Id,
                EvaluatorId = eval1.Id,
                MetricName = eval1.Name,
                Score = 5.0,
                EvaluatedAt = DateTime.UtcNow
            }, ct);
            await context.SaveChangesAsync(ct);
        }

        var request = new MessagesMetadataRequest { MessageIds = [messageId] };

        // Act
        var response = await Client.PostAsJsonAsync("/messages/metadata", request, ct);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MessagesMetadataResponse>(ct);

        result.ShouldNotBeNull();
        result.HasMissingEvaluators.ShouldContainKey(messageId);
        result.HasMissingEvaluators[messageId].ShouldBeTrue();
    }

    [Fact]
    public async Task GetMessagesMetadata_ReportsNoMissingEvaluators_WhenAllEvaluatorsRun()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        int messageId;

        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();

            // Add two evaluators
            var eval1 = new Evaluator { Name = "Evaluator 1" };
            var eval2 = new Evaluator { Name = "Evaluator 2" };
            await context.Evaluators.AddRangeAsync([eval1, eval2], ct);

            // Add one message
            var msg = new Message
            {
                Text = "Test message",
                PlayerId = null,
                IsScriptedMessage = false,
                GameId = Guid.NewGuid(),
                AgentId = "test-agent",
                InstructionVersionId = 1
            };
            await context.Messages.AddAsync(msg, ct);
            await context.SaveChangesAsync(ct);
            messageId = msg.Id;

            // Add metrics for BOTH evaluators
            await context.MessageEvaluationMetrics.AddAsync(new MessageEvaluationMetric
            {
                MessageId = msg.Id,
                EvaluatorId = eval1.Id,
                MetricName = eval1.Name,
                Score = 5.0,
                EvaluatedAt = DateTime.UtcNow
            }, ct);
            await context.MessageEvaluationMetrics.AddAsync(new MessageEvaluationMetric
            {
                MessageId = msg.Id,
                EvaluatorId = eval2.Id,
                MetricName = eval2.Name,
                Score = 5.0,
                EvaluatedAt = DateTime.UtcNow
            }, ct);
            await context.SaveChangesAsync(ct);
        }

        var request = new MessagesMetadataRequest { MessageIds = [messageId] };

        // Act
        var response = await Client.PostAsJsonAsync("/messages/metadata", request, ct);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<MessagesMetadataResponse>(ct);

        result.ShouldNotBeNull();
        result.HasMissingEvaluators.ShouldContainKey(messageId);
        result.HasMissingEvaluators[messageId].ShouldBeFalse();
    }
}
