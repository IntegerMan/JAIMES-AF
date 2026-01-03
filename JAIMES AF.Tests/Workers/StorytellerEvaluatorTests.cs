using MattEland.Jaimes.Evaluators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class StorytellerEvaluatorTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly StorytellerEvaluator _evaluator;

    public StorytellerEvaluatorTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _evaluator = new StorytellerEvaluator(_mockChatClient.Object);
    }

    [Fact]
    public void EvaluationMetricNames_ShouldReturnStoryteller()
    {
        // Act
        var metricNames = _evaluator.EvaluationMetricNames;

        // Assert
        metricNames.ShouldContain(StorytellerEvaluator.MetricName);
        metricNames.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_WithValidS0S1S2Tags_ShouldParseCorrectly()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: The narrative shows good progression with meaningful developments. Tension is present and fair.</S0>
                              <S1>The storytelling maintains engagement with good pacing and clear hooks.</S1>
                              <S2>5</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            "The ancient artifact glows with an otherworldly light as you approach."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "I approach the altar.")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldContain("engagement");

        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics.ShouldContain(d => d.Message.Contains("Storyteller Score: 5 (Pass)"));
        metric.Diagnostics.ShouldContain(d => d.Message.Contains("ThoughtChain:"));

        metric.Interpretation.ShouldNotBeNull();
        metric.Interpretation.Rating.ShouldBe(EvaluationRating.Good);
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
        metric.Reason.ShouldBe("Explanation here.");
    }

    [Fact]
    public async Task EvaluateAsync_WithMultipleExchanges_ShouldAnalyzeConversationTrends()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: Analyzing 3 exchanges, the narrative shows consistent progression.</S0>
                              <S1>Good storytelling with building tension across exchanges.</S1>
                              <S2>4</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((msgs, opts, ct) =>
            {
                // Verify conversation history is included with exchange markers
                var userMessage = msgs.FirstOrDefault(m => m.Role == ChatRole.User);
                userMessage.ShouldNotBeNull();
                userMessage!.Text.ShouldContain("Exchange 1");
                userMessage.Text.ShouldContain("Exchange 2");
                userMessage.Text.ShouldContain("Exchange 3");
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The dragon roars in fury!"));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "I enter the cave."),
            new(ChatRole.Assistant, "The cave is dark and foreboding."),
            new(ChatRole.User, "I light a torch."),
            new(ChatRole.Assistant, "The torch reveals ancient carvings on the walls."),
            new(ChatRole.User, "I examine the carvings.")
            // Removed trailing assistant message - the GM response is modelResponse
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);

        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics.ShouldContain(d => d.Message.Contains("Exchanges analyzed: 3"));

        metric.Interpretation.ShouldNotBeNull();
        metric.Interpretation.Rating.ShouldBe(EvaluationRating.Good);
    }

    [Fact]
    public async Task EvaluateAsync_WithFewExchanges_ShouldAddWarningDiagnostic()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: Limited context available.</S0>
                              <S1>Adequate storytelling but limited exchanges.</S1>
                              <S2>4</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "You see a door."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "I look around.")
            // Removed trailing assistant message
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4); // Score should not be affected

        // Should have warning about limited context
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Limited conversation context"));
        warningDiagnostic.ShouldNotBeNull();
        warningDiagnostic.Message.ShouldContain("1 exchange(s)"); // 1 User message = 1 exchange
    }

    [Fact]
    public async Task EvaluateAsync_WithNoExchanges_ShouldAddWarningDiagnostic()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: No prior context available.</S0>
                              <S1>Cannot fully evaluate without context.</S1>
                              <S2>3</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Welcome, adventurer."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master.")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();

        // Should have warning about limited context
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Limited conversation context"));
        warningDiagnostic.ShouldNotBeNull();
        warningDiagnostic.Message
            .ShouldContain("0 exchange(s)"); // 0 User messages = 0 exchanges
    }

    [Fact]
    public async Task EvaluateAsync_WithMultilineContent_ShouldParseCorrectly()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step:
                              First, I analyze the narrative momentum.
                              Then, I check for tension and challenge.
                              Finally, I evaluate pacing.</S0>
                              <S1>This is a multi-line explanation that should be parsed correctly.</S1>
                              <S2>3</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
            // Removed trailing assistant message
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldContain("multi-line explanation");

        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldNotBeNull();
        thoughtChainDiagnostic!.Message.ShouldContain("First, I analyze");
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
            // Removed trailing assistant message
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
            // Removed trailing assistant message
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score when parsing fails
        metric.Reason.ShouldBe("Explanation here.");

        // Should have warning about parsing failure
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Failed to parse evaluation response"));
        warningDiagnostic.ShouldNotBeNull();
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Pass response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);

        var passDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Pass)"));
        passDiagnostic.ShouldNotBeNull();
        passDiagnostic.Message.ShouldContain("Storyteller Score: 4 (Pass)");
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
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Fail response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);

        var failDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Fail)"));
        failDiagnostic.ShouldNotBeNull();
        failDiagnostic.Message.ShouldContain("Storyteller Score: 3 (Fail)");

        metric.Interpretation.ShouldNotBeNull();
        metric.Interpretation.Rating.ShouldBe(EvaluationRating.Poor);
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyResponse_ShouldUseDefaultValues()
    {
        // Arrange
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score
        metric.Reason.ShouldBe("Failed to parse evaluation response.");

        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Failed to parse evaluation response"));
        warningDiagnostic.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithNullResponse_ShouldUseDefaultValues()
    {
        // Arrange
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, (string?)null)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score
        metric.Reason.ShouldBe("Failed to parse evaluation response.");

        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Failed to parse evaluation response. Raw response: {Empty}"));
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
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((msgs, opts, ct) =>
            {
                // Verify system prompt is included
                var userMessage = msgs.FirstOrDefault(m => m.Role == ChatRole.User);
                userMessage.ShouldNotBeNull();
                userMessage!.Text.ShouldContain("System Instructions for the Game Master:");
                userMessage.Text.ShouldContain("You are a test game master.");
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a test game master."),
            new(ChatRole.User, "Test")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
    }

    [Fact]
    public async Task EvaluateAsync_WithFiveExchanges_ShouldNotAddWarningDiagnostic()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: Full context available for analysis.</S0>
                              <S1>Excellent storytelling with consistent quality.</S1>
                              <S2>5</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The final response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "Exchange 1 user"),
            new(ChatRole.Assistant, "Exchange 1 assistant"),
            new(ChatRole.User, "Exchange 2 user"),
            new(ChatRole.Assistant, "Exchange 2 assistant"),
            new(ChatRole.User, "Exchange 3 user"),
            new(ChatRole.Assistant, "Exchange 3 assistant"),
            new(ChatRole.User, "Exchange 4 user"),
            new(ChatRole.Assistant, "Exchange 4 assistant"),
            new(ChatRole.User, "Exchange 5 user")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);

        // Should have exchanges analyzed diagnostic
        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics!.ShouldContain(d =>
            d.Message.Contains("Exchanges analyzed: 5")); // Capped at TargetExchangeCount (5)

        // Should NOT have warning about limited context
        var warningDiagnostic = metric.Diagnostics.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Limited conversation context"));
        warningDiagnostic.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithThreeExchanges_ShouldNotAddWarningDiagnostic()
    {
        // Arrange - 3 exchanges is the minimum threshold, should NOT trigger warning
        string responseText = """
                              <S0>Let's think step by step: Adequate context for analysis.</S0>
                              <S1>Good storytelling observed.</S1>
                              <S2>4</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<List<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Third response."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "First message"),
            new(ChatRole.Assistant, "First response"),
            new(ChatRole.User, "Second message"),
            new(ChatRole.Assistant, "Second response"),
            new(ChatRole.User, "Third message")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();

        // Should have exchanges analyzed diagnostic
        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics!.ShouldContain(d => d.Message.Contains("Exchanges analyzed: 3"));

        // Should NOT have warning about limited context (3 is the threshold)
        var warningDiagnostic = metric.Diagnostics.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("Limited conversation context"));
        warningDiagnostic.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithMoreThanFiveExchanges_ShouldOnlyAnalyzeLastFive()
    {
        // Arrange
        string responseText = """
                              <S0>Let's think step by step: Analysis of recent exchanges.</S0>
                              <S1>Good storytelling in recent interactions.</S1>
                              <S2>4</S2>
                              """;

        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((msgs, opts, ct) =>
            {
                var userMessage = msgs.FirstOrDefault(m => m.Role == ChatRole.User);
                userMessage.ShouldNotBeNull();
                // Should only contain exchanges 4-8 (the last 5), not exchanges 1-3
                userMessage!.Text.ShouldNotContain("Very old exchange");
                userMessage.Text.ShouldContain("Exchange 4");
                userMessage.Text.ShouldContain("Exchange 8");
            })
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Exchange 8 response"));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Very old exchange 1"),
            new(ChatRole.Assistant, "Very old response 1"),
            new(ChatRole.User, "Very old exchange 2"),
            new(ChatRole.Assistant, "Very old response 2"),
            new(ChatRole.User, "Very old exchange 3"),
            new(ChatRole.Assistant, "Very old response 3"),
            new(ChatRole.User, "Exchange 4"),
            new(ChatRole.Assistant, "Exchange 4 response"),
            new(ChatRole.User, "Exchange 5"),
            new(ChatRole.Assistant, "Exchange 5 response"),
            new(ChatRole.User, "Exchange 6"),
            new(ChatRole.Assistant, "Exchange 6 response"),
            new(ChatRole.User, "Exchange 7"),
            new(ChatRole.Assistant, "Exchange 7 response"),
            new(ChatRole.User, "Exchange 8")
        };

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(StorytellerEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics!.ShouldContain(d => d.Message.Contains("Exchanges analyzed: 5")); // Capped at 5
    }
}
