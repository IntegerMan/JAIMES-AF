using MattEland.Jaimes.Evaluators;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

public class GameMechanicsEvaluatorTests
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<IRulesSearchService> _mockRulesSearchService;
    private readonly GameMechanicsEvaluator _evaluator;

    public GameMechanicsEvaluatorTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockRulesSearchService = new Mock<IRulesSearchService>();
        Mock<ILogger<GameMechanicsEvaluator>> mockLogger = new();
        Mock<IConfiguration> mockConfiguration = new();
        _evaluator =
            new GameMechanicsEvaluator(_mockChatClient.Object, _mockRulesSearchService.Object, mockLogger.Object,
                mockConfiguration.Object);
    }

    [Fact]
    public void EvaluationMetricNames_ShouldReturnGameMechanics()
    {
        // Act
        var metricNames = _evaluator.EvaluationMetricNames;

        // Assert
        metricNames.ShouldContain(GameMechanicsEvaluator.MetricName);
        metricNames.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EvaluateAsync_WithoutContext_ShouldRunAndReturnWarningDiagnostic()
    {
        // Arrange
        string topicsResponse = "damage calculation";
        string evaluationResponse = """
                                    <S0>Evaluation without specific ruleset context.</S0>
                                    <S1>Response evaluated against all available rules.</S1>
                                    <S2>4</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "You deal 1d8+3 damage."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I attack!") };

        // Act - No evaluation context provided
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Should still evaluate but with warning diagnostic
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4); // Evaluation should succeed

        // Should have warning diagnostic about missing context
        var warningDiagnostic = metric.Diagnostics?.FirstOrDefault(d =>
            d.Severity == EvaluationDiagnosticSeverity.Warning &&
            d.Message.Contains("No GameMechanicsEvaluationContext provided"));
        warningDiagnostic.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithValidS0S1S2Tags_ShouldParseCorrectly()
    {
        // Arrange
        string topicsResponse = "damage calculation";
        string evaluationResponse = """
                                    <S0>Let's think step by step: The response correctly applies damage calculation rules. The attack roll was made before damage, and the damage dice used are correct for a longsword.</S0>
                                    <S1>The response follows game mechanics correctly with proper attack and damage procedures.</S1>
                                    <S2>5</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            "You swing your longsword, rolling a 15 to hit against the goblin's AC of 12. The attack connects, dealing 1d8+3 slashing damage."));
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a game master."),
            new(ChatRole.User, "I attack the goblin with my longsword.")
        };

        var context = new GameMechanicsEvaluationContext("test-ruleset", "Test Ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
        metric.Reason.ShouldNotBeNull();
        metric.Reason.ShouldContain("follows game mechanics correctly");

        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics!.ShouldContain(d => d.Message.Contains("GameMechanics Score: 5 (Pass)"));
        metric.Diagnostics!.ShouldContain(d => d.Message.Contains("ThoughtChain:"));

        metric.Interpretation.ShouldNotBeNull();
        metric.Interpretation.Rating.ShouldBe(EvaluationRating.Good);
    }

    [Fact]
    public async Task EvaluateAsync_WithCaseInsensitiveTags_ShouldParseCorrectly()
    {
        // Arrange
        string topicsResponse = "spell casting";
        string evaluationResponse = """
                                    <s0>Let's think step by step: Analysis here.</s0>
                                    <s1>Explanation here.</s1>
                                    <s2>4</s2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I cast fireball.") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
        metric.Reason.ShouldBe("Explanation here.");
    }

    [Fact]
    public async Task EvaluateAsync_WithMultilineContent_ShouldParseCorrectly()
    {
        // Arrange
        string topicsResponse = "movement";
        string evaluationResponse = """
                                    <S0>Let's think step by step:
                                    First, I analyze the combat mechanics in the response.
                                    Then, I check for rule adherence.
                                    Finally, I assign a score.</S0>
                                    <S1>This is a multi-line explanation that should be parsed correctly.</S1>
                                    <S2>3</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I move 30 feet.") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);
        metric.Reason.ShouldNotBeNull();
        metric.Reason!.ShouldContain("multi-line explanation");

        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldNotBeNull();
        thoughtChainDiagnostic!.Message.ShouldContain("First, I analyze");
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingS0Tag_ShouldStillParseS1AndS2()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S1>Explanation without ThoughtChain.</S1>
                                    <S2>2</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
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
        string topicsResponse = "combat";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis here.</S0>
                                    <S1>Explanation here.</S1>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score when parsing fails
        metric.Reason.ShouldBe("Explanation here.");

        // Should have warning about parsing failure
        var warningDiagnostic =
            metric.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Warning);
        warningDiagnostic.ShouldNotBeNull();
        warningDiagnostic!.Message.ShouldContain("Failed to parse evaluation response");
    }

    [Fact]
    public async Task EvaluateAsync_WithInvalidScore_ShouldClampToValidRange()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>10</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5); // Clamped to max
    }

    [Fact]
    public async Task EvaluateAsync_WithNegativeScore_ShouldClampToValidRange()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>-5</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Clamped to min
    }

    [Fact]
    public async Task EvaluateAsync_WithScore4_ShouldMarkAsPass()
    {
        // Arrange
        string topicsResponse = "skill check";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>4</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I roll perception.") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);

        var passDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Pass)"));
        passDiagnostic.ShouldNotBeNull();
        passDiagnostic!.Message.ShouldContain("GameMechanics Score: 4 (Pass)");
    }

    [Fact]
    public async Task EvaluateAsync_WithScore3_ShouldMarkAsFail()
    {
        // Arrange
        string topicsResponse = "saving throw";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>3</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I make a saving throw.") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(3);

        var failDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("(Fail)"));
        failDiagnostic.ShouldNotBeNull();
        failDiagnostic!.Message.ShouldContain("GameMechanics Score: 3 (Fail)");

        metric.Interpretation.ShouldNotBeNull();
        metric.Interpretation.Rating.ShouldBe(EvaluationRating.Poor);
    }

    [Fact]
    public async Task EvaluateAsync_WithEmptyResponse_ShouldUseDefaultValues()
    {
        // Arrange - Both calls return empty (first for topics, second for evaluation)
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score
        metric.Reason.ShouldBe("Failed to parse evaluation response.");

        var warningDiagnostic =
            metric.Diagnostics?.FirstOrDefault(d => d.Severity == EvaluationDiagnosticSeverity.Warning);
        warningDiagnostic.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithNonNumericScore_ShouldUseDefaultScore()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>invalid</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(1); // Default score when parsing fails
    }

    [Fact]
    public async Task EvaluateAsync_WithWhitespaceInTags_ShouldTrimCorrectly()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>   Let's think step by step: Analysis with spaces.   </S0>
                                    <S1>   Explanation with spaces.   </S1>
                                    <S2>   5   </S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
        metric.Reason.ShouldBe("Explanation with spaces.");

        var thoughtChainDiagnostic = metric.Diagnostics?.FirstOrDefault(d => d.Message.Contains("ThoughtChain:"));
        thoughtChainDiagnostic.ShouldNotBeNull();
        thoughtChainDiagnostic.Message.ShouldContain("Analysis with spaces");
        thoughtChainDiagnostic.Message.ShouldNotContain("   "); // Should be trimmed
    }

    [Fact]
    public async Task EvaluateAsync_ShouldSearchRulesForIdentifiedTopics()
    {
        // Arrange
        string topicsResponse = "damage calculation\nattack rolls";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>5</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);
        SetupMockRulesSearchForAnyQuery(CreateSampleRuleResults());

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "You hit for 1d8+3 damage."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I attack the goblin.") };
        var context = new GameMechanicsEvaluationContext("test-ruleset", "Test Ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _mockRulesSearchService.Verify(
            x => x.SearchRulesDetailedAsync("test-ruleset", "damage calculation", false, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRulesSearchService.Verify(
            x => x.SearchRulesDetailedAsync("test-ruleset", "attack rolls", false, It.IsAny<CancellationToken>()),
            Times.Once);

        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Diagnostics.ShouldNotBeNull();
        metric.Diagnostics!.ShouldContain(d => d.Message.Contains("Mechanics topics identified:"));
    }

    [Fact]
    public async Task EvaluateAsync_WithNarrativeResponse_ShouldIdentifyNoTopics()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>Let's think step by step: The response is purely narrative with no mechanical claims.</S0>
                                    <S1>No game mechanics are referenced, so there are no rules to violate.</S1>
                                    <S2>5</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            "The tavern is dimly lit, with flickering candles casting long shadows across the worn wooden tables."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "What does the tavern look like?") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // No rules should be searched when there are no mechanics topics
        _mockRulesSearchService.Verify(
            x => x.SearchRulesDetailedAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(5);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldIncludeContextInMetric()
    {
        // Arrange
        string topicsResponse = "NONE";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Analysis.</S0>
                                    <S1>Explanation.</S1>
                                    <S2>5</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Test response."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "Test") };
        var context = new GameMechanicsEvaluationContext("my-ruleset-id", "My Custom Ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Context.ShouldNotBeNull();
        metric.Context.ShouldContainKey(GameMechanicsEvaluationContext.ContextName);

        var includedContext =
            metric.Context[GameMechanicsEvaluationContext.ContextName] as GameMechanicsEvaluationContext;
        includedContext.ShouldNotBeNull();
        includedContext!.RulesetId.ShouldBe("my-ruleset-id");
        includedContext.RulesetName.ShouldBe("My Custom Ruleset");
    }

    [Fact]
    public async Task EvaluateAsync_WhenRulesSearchFails_ShouldContinueEvaluation()
    {
        // Arrange
        string topicsResponse = "combat";
        string evaluationResponse = """
                                    <S0>Let's think step by step: Unable to find specific rules, but response seems reasonable.</S0>
                                    <S1>No specific rules found for this context.</S1>
                                    <S2>4</S2>
                                    """;

        SetupMockChatClientSequence(topicsResponse, evaluationResponse);

        _mockRulesSearchService
            .Setup(x => x.SearchRulesDetailedAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Search service unavailable"));

        var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, "You attack the enemy."));
        var messages = new List<ChatMessage> { new(ChatRole.User, "I attack!") };
        var context = new GameMechanicsEvaluationContext("test-ruleset");

        // Act
        var result = await _evaluator.EvaluateAsync(messages, modelResponse,
            evaluationContext: [context],
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Should still produce a result even if rules search failed
        var metric = result.Get<NumericMetric>(GameMechanicsEvaluator.MetricName);
        metric.ShouldNotBeNull();
        metric.Value.ShouldBe(4);
    }

    [Fact]
    public void GameMechanicsEvaluationContext_ShouldHaveCorrectName()
    {
        // Arrange & Act
        var context = new GameMechanicsEvaluationContext("test-id", "Test Name");

        // Assert
        context.Name.ShouldBe(GameMechanicsEvaluationContext.ContextName);
        context.RulesetId.ShouldBe("test-id");
        context.RulesetName.ShouldBe("Test Name");
    }

    [Fact]
    public void GameMechanicsEvaluationContext_ContentsIncludesRulesetInfo()
    {
        // Arrange & Act
        var context = new GameMechanicsEvaluationContext("test-id", "Test Ruleset");

        // Assert
        context.Contents.ShouldNotBeNull();
        context.Contents.ShouldNotBeEmpty();

        var textContent = context.Contents.OfType<TextContent>().FirstOrDefault();
        textContent.ShouldNotBeNull();
        textContent.Text.ShouldContain("Test Ruleset");
        textContent.Text.ShouldContain("test-id");
    }

    [Fact]
    public void GameMechanicsEvaluationContext_WithoutName_ContentsIncludesOnlyId()
    {
        // Arrange & Act
        var context = new GameMechanicsEvaluationContext("only-id-provided");

        // Assert
        context.Contents.ShouldNotBeNull();

        var textContent = context.Contents.OfType<TextContent>().FirstOrDefault();
        textContent.ShouldNotBeNull();
        textContent.Text.ShouldContain("only-id-provided");
    }

    [Fact]
    public void GameMechanicsEvaluationContext_WithNullRulesetId_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new GameMechanicsEvaluationContext(null!));
    }

    #region Helper Methods

    private void SetupMockChatClientSequence(string topicsResponse, string evaluationResponse)
    {
        // Set up the sequence for both topic identification and evaluation
        _mockChatClient
            .SetupSequence(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, topicsResponse)))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, evaluationResponse)));
    }

    private void SetupMockForTopicIdentification(string topicsResponse)
    {
        // Simple setup that returns the same response for all calls
        // Used when we only care about topic identification for testing
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, topicsResponse)));
    }

    private void SetupMockEvaluationResponse(string responseText)
    {
        // Override previous setup with a new response
        _mockChatClient
            .Setup(x => x.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
    }

    private void SetupMockRulesSearch(string query, SearchRuleResult[] results)
    {
        _mockRulesSearchService
            .Setup(x => x.SearchRulesDetailedAsync(It.IsAny<string?>(), query, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = results });
    }

    private void SetupMockRulesSearchForAnyQuery(SearchRuleResult[] results)
    {
        _mockRulesSearchService
            .Setup(x => x.SearchRulesDetailedAsync(It.IsAny<string?>(), It.IsAny<string>(), false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchRulesResponse { Results = results });
    }

    private static SearchRuleResult[] CreateSampleRuleResults()
    {
        return
        [
            new SearchRuleResult
            {
                Text = "When you make an attack, roll a d20 and add your attack modifier.",
                DocumentId = "doc-1",
                DocumentName = "Combat Rules",
                RulesetId = "test-ruleset",
                EmbeddingId = "emb-1",
                ChunkId = "chunk-1",
                Relevancy = 0.95
            },
            new SearchRuleResult
            {
                Text = "A longsword deals 1d8 slashing damage (or 1d10 if used two-handed).",
                DocumentId = "doc-2",
                DocumentName = "Weapon Stats",
                RulesetId = "test-ruleset",
                EmbeddingId = "emb-2",
                ChunkId = "chunk-2",
                Relevancy = 0.85
            }
        ];
    }

    #endregion
}
