using MattEland.Jaimes.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class BrevityEvaluatorTests
{
    [Theory]
    [InlineData(500, 5)] // Perfect
    [InlineData(400, 5)] // On margin (low)
    [InlineData(600, 5)] // On margin (high)
    [InlineData(601, 4)] // Just outside margin (high)
    [InlineData(399, 4)] // Just outside margin (low)
    [InlineData(700, 4)] // Exactly one margin over
    [InlineData(300, 4)] // Exactly one margin under
    [InlineData(701, 3)] // Just outside two margins over
    [InlineData(299, 3)] // Just outside two margins under
    [InlineData(1000, 1)] // Very over
    [InlineData(10, 1)] // Very under
    public async Task EvaluateAsync_ShouldReturnCorrectScore(int charCount, int expectedScore)
    {
        // Arrange
        var options = Options.Create(new BrevityEvaluatorOptions
        {
            TargetCharacters = 500,
            Margin = 100
        });
        var evaluator = new BrevityEvaluator(options);

        var text = new string('a', charCount);
        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act
        var result =
            await evaluator.EvaluateAsync([], modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>("Brevity");
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(expectedScore);
    }

    [Fact]
    public async Task EvaluateAsync_WithZeroMargin_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = Options.Create(new BrevityEvaluatorOptions
        {
            TargetCharacters = 500,
            Margin = 0
        });
        var evaluator = new BrevityEvaluator(options);

        var text = new string('a', 501);
        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await evaluator.EvaluateAsync([], modelResponse, cancellationToken: TestContext.Current.CancellationToken));
    }
}
