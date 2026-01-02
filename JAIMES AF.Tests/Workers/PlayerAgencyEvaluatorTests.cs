using MattEland.Jaimes.Workers.AssistantMessageWorker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class PlayerAgencyEvaluatorTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly PlayerAgencyEvaluator _evaluator;

    public PlayerAgencyEvaluatorTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _evaluator = new PlayerAgencyEvaluator(_mockChatClient.Object);
    }

    [Fact]
    public void EvaluationMetricNames_ShouldReturnPlayerAgency()
    {
        // Act
        var metricNames = _evaluator.EvaluationMetricNames;

        // Assert
        metricNames.ShouldContain(PlayerAgencyEvaluator.MetricName);
        metricNames.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_WithValidS0S1S2Tags_ShouldParseCorrectly()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: The response describes the world without taking actions for the player. It presents the situation and allows the player to decide what to do.</S0>
            <S1>The response respects player agency by describing the environment without assumptions.</S1>
            <S2>5</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The door stands before you."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "What do I see?")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldContain("respects player agency");
        
        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics.ShouldContain(d => d.Message.Contains("Player Agency Score: 5 (Pass)"));
        metric.Diagnostics.ShouldContain(d => d.Message.Contains("ThoughtChain:"));
    }

    [Fact]
    public async Task EvaluateAsync_WithCaseInsensitiveTags_ShouldParseCorrectly()
    {
        // Arrange
        string responseText = """
            <s0>Let's think step by step: Analysis here.</s0>
            <s1>Explanation here.</s1>
            <s2>4</s2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldBe("Explanation here.");
    }

    [Fact]
    public async Task EvaluateAsync_WithMultilineContent_ShouldParseCorrectly()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step:
            First, I analyze the response.
            Then, I check for agency issues.
            Finally, I assign a score.</S0>
            <S1>This is a multi-line explanation that should be parsed correctly.</S1>
            <S2>3</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldContain("multi-line explanation");
        
        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldNotBeNull();
        thoughtChainDiagnostic!.Message.ShouldNotBeNull();
        thoughtChainDiagnostic.Message.ShouldContain("First, I analyze");
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingS0Tag_ShouldStillParseS1AndS2()
    {
        // Arrange
        string responseText = """
            <S1>Explanation without ThoughtChain.</S1>
            <S2>2</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(2);
        metric.Reason.ShouldBe("Explanation without ThoughtChain.");
        
        // Should not have ThoughtChain diagnostic
        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingS2Tag_ShouldUseDefaultScore()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis here.</S0>
            <S1>Explanation here.</S1>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score when parsing fails
        metric.Reason.ShouldBe("Explanation here.");
        
        // Should have warning about parsing failure
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Warning);
        warningDiagnostic.ShouldNotBeNull();
        warningDiagnostic.Message.ShouldContain("Failed to parse evaluation response");
    }

    [Fact]
    public async Task EvaluateAsync_WithInvalidScore_ShouldClampToValidRange()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>10</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5); // Clamped to max
    }

    [Fact]
    public async Task EvaluateAsync_WithNegativeScore_ShouldClampToValidRange()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>-5</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Clamped to min
    }

    [Fact]
    public async Task EvaluateAsync_WithScore4_ShouldMarkAsPass()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>4</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
        
        var passDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Pass)"));
        passDiagnostic.ShouldNotBeNull();
        passDiagnostic.Message.ShouldContain("Player Agency Score: 4 (Pass)");
    }

    [Fact]
    public async Task EvaluateAsync_WithScore3_ShouldMarkAsFail()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>3</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);
        
        var failDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Fail)"));
        failDiagnostic.ShouldNotBeNull();
        failDiagnostic.Message.ShouldContain("Player Agency Score: 3 (Fail)");
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyResponse_ShouldUseDefaultValues()
    {
        // Arrange
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score
        metric.Reason.ShouldBe("Failed to parse evaluation response.");
        
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Warning);
        warningDiagnostic.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithSystemPrompt_ShouldIncludeInPrompt()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>5</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((msgs, opts, ct) =>
            {
                // Verify system prompt is included
                var userMessage = msgs.FirstOrDefault(m => m.Role == ChatRole.User);
                userMessage.ShouldNotBeNull();
                userMessage!.Text.ShouldContain("System Instructions:");
                userMessage.Text.ShouldContain("You are a test game master.");
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a test game master."),
            new(ChatRole.User, "What do I see?")
        };

        // Act
        await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert - verified in callback
        _mockChatClient.Verify(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_WithConversationMessages_ShouldIncludeInPrompt()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>5</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((msgs, opts, ct) =>
            {
                // Verify conversation context is included
                var userMessage = msgs.FirstOrDefault(m => m.Role == ChatRole.User);
                userMessage.ShouldNotBeNull();
                userMessage!.Text.ShouldContain("Conversation Context:");
                userMessage.Text.ShouldContain("User: Hello");
                userMessage.Text.ShouldContain("Assistant: Hi there");
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "Hello"),
            new ChatMessage(ChatRole.Assistant, "Hi there")
        };

        // Act
        await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert - verified in callback
        _mockChatClient.Verify(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_WithNonNumericScore_ShouldUseDefaultScore()
    {
        // Arrange
        string responseText = """
            <S0>Let's think step by step: Analysis.</S0>
            <S1>Explanation.</S1>
            <S2>invalid</S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score when parsing fails
    }

    [Fact]
    public async Task EvaluateAsync_WithWhitespaceInTags_ShouldTrimCorrectly()
    {
        // Arrange
        string responseText = """
            <S0>   Let's think step by step: Analysis with spaces.   </S0>
            <S1>   Explanation with spaces.   </S1>
            <S2>   5   </S2>
            """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(PlayerAgencyEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
        metric.Reason.ShouldBe("Explanation with spaces.");
        
        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldNotBeNull();
        thoughtChainDiagnostic.Message.ShouldContain("Analysis with spaces");
        thoughtChainDiagnostic.Message.ShouldNotContain("   "); // Should be trimmed
    }
}

