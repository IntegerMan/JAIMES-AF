using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Moq.Protected;
using System.Net.Http.Json;

namespace MattEland.Jaimes.Tests.Components;

public class ToolCallFullDetailsTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    public ToolCallFullDetailsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton(_loggerFactoryMock.Object);

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
    }

    private void SetupMudProviders()
    {
        RenderComponent<MudThemeProvider>();
        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
        RenderComponent<MudSnackbarProvider>();
    }

    private static ToolCallFullDetailResponse CreateTestToolCallDetail(
        int id = 42,
        string toolName = "RulesSearch",
        Guid? gameId = null)
    {
        return new ToolCallFullDetailResponse
        {
            Id = id,
            ToolName = toolName,
            CreatedAt = DateTime.UtcNow,
            MessageId = 100,
            GameId = gameId ?? Guid.NewGuid(),
            GameName = "Epic Adventure - Hero Player",
            AgentId = "game-master",
            InstructionVersionId = 1,
            AgentName = "Game Master",
            AgentVersion = "1.0",
            FeedbackIsPositive = true,
            FeedbackComment = "Great response!",
            InputJson = "{\"query\": \"search rules\"}",
            OutputJson = "{\"results\": [\"Rule 1\", \"Rule 2\"]}",
            ContextMessages = []
        };
    }

    private void SetupHttpClientFactory(ToolCallFullDetailResponse? response)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/admin/tool-calls/")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = response != null ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound,
                Content = response != null ? JsonContent.Create(response) : null,
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Api")).Returns(httpClient);
        Services.AddSingleton(httpClientFactoryMock.Object);
    }

    [Fact]
    public void ToolCallFullDetails_ShouldRenderHeroSection_WhenDataLoaded()
    {
        // Arrange
        var response = CreateTestToolCallDetail(id: 42, toolName: "RulesSearch");
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Hero section with tool name should be present
        cut.Markup.ShouldContain("Tool Call: RulesSearch");
        cut.Markup.ShouldContain("Call #42");
    }

    [Fact]
    public void ToolCallFullDetails_ShouldShowViewGameButton_WhenGameExists()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var response = CreateTestToolCallDetail(id: 42, gameId: gameId);
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - View Game button should be present in hero section
        cut.Markup.ShouldContain("View Game");
        cut.Markup.ShouldContain($"/games/{gameId}");
    }

    [Fact]
    public void ToolCallFullDetails_ShouldNotShowViewGameButton_WhenNoGame()
    {
        // Arrange
        var response = new ToolCallFullDetailResponse
        {
            Id = 42,
            ToolName = "RulesSearch",
            CreatedAt = DateTime.UtcNow,
            MessageId = 100,
            GameId = null, // No game associated
            GameName = null,
            AgentId = "game-master",
            InstructionVersionId = 1,
            AgentName = "Game Master",
            AgentVersion = "1.0",
            FeedbackIsPositive = null,
            FeedbackComment = null,
            InputJson = "{}",
            OutputJson = "{}",
            ContextMessages = []
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - View Game button should NOT be in hero action
        // The action button in hero section should not render when no game
        var buttons = cut.FindComponents<MudButton>();
        var viewGameButton = buttons.FirstOrDefault(b => b.Markup.Contains("View Game"));
        viewGameButton.ShouldBeNull("View Game button should not appear when no game is associated");
    }

    [Fact]
    public void ToolCallFullDetails_ShouldShowErrorState_WhenToolCallNotFound()
    {
        // Arrange
        SetupHttpClientFactory(null);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 999));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Error message should be visible
        cut.Markup.ShouldContain("Tool call not found");
    }

    [Fact]
    public void ToolCallFullDetails_ShouldShowInputAndOutputJson()
    {
        // Arrange
        var response = CreateTestToolCallDetail();
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Input and output sections should be visible
        cut.Markup.ShouldContain("Input Payload");
        cut.Markup.ShouldContain("Output Result");
        cut.Markup.ShouldContain("search rules"); // Part of input JSON
        cut.Markup.ShouldContain("Rule 1"); // Part of output JSON
    }

    [Fact]
    public void ToolCallFullDetails_ShouldShowConversationContext()
    {
        // Arrange
        var response = CreateTestToolCallDetail();
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Conversation context section should be visible
        cut.Markup.ShouldContain("Conversation Context");
    }

    [Fact]
    public void ToolCallFullDetails_ShouldHaveBreadcrumbs()
    {
        // Arrange
        var response = CreateTestToolCallDetail(toolName: "TestTool");
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Breadcrumbs should be present with tool name
        var breadcrumbs = cut.FindComponent<MudBreadcrumbs>();
        breadcrumbs.ShouldNotBeNull();
        cut.Markup.ShouldContain("TestTool"); // Tool name in breadcrumb
    }

    [Fact]
    public void ToolCallFullDetails_ShouldShowLoadingState_Initially()
    {
        // Arrange - Setup a slow response
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(async () =>
            {
                await Task.Delay(5000); // Simulate slow response
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = JsonContent.Create(CreateTestToolCallDetail()),
                };
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Api")).Returns(httpClient);
        Services.AddSingleton(httpClientFactoryMock.Object);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolCallFullDetails>(parameters => parameters
            .Add(p => p.Id, 42));

        // Assert - Loading indicator should be present initially
        cut.FindAll(".mud-progress-circular").ShouldNotBeEmpty("Loading indicator should be shown while loading");
    }
}
