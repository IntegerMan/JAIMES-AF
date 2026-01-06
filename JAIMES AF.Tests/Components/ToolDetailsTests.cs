using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.Web.Components.Shared;
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

public class ToolDetailsTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public ToolDetailsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton(_loggerFactoryMock.Object);
        Services.AddSingleton(_dialogServiceMock.Object);

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);
    }

    private void SetupMudProviders()
    {
        RenderComponent<MudThemeProvider>();
        RenderComponent<MudPopoverProvider>();
        RenderComponent<MudDialogProvider>();
        RenderComponent<MudSnackbarProvider>();
    }

    private static ToolCallDetailDto CreateTestToolCall(
        int id = 1,
        string toolName = "RulesSearch",
        Guid? gameId = null)
    {
        return new ToolCallDetailDto
        {
            Id = id,
            ToolName = toolName,
            CreatedAt = DateTime.UtcNow,
            MessageId = 100,
            GameId = gameId ?? Guid.NewGuid(),
            GameName = "Test Game",
            AgentId = "game-master",
            InstructionVersionId = 1,
            AgentName = "Game Master",
            AgentVersion = "1.0",
            FeedbackIsPositive = true,
            FeedbackComment = "Great response!"
        };
    }

    private void SetupHttpClientFactory(ToolCallDetailListResponse response)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/admin/tools/")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(response),
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Api")).Returns(httpClient);
        Services.AddSingleton(httpClientFactoryMock.Object);
    }

    [Fact]
    public void ToolDetailsPage_ShouldRenderHeroSection()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [CreateTestToolCall()],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            ToolName = "RulesSearch",
            ToolDescription = "Search through game rules"
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "RulesSearch"));

        // Assert - Hero section with tool name should be present
        cut.Markup.ShouldContain("RulesSearch");
        cut.Markup.ShouldContain("All Tools"); // Action button text
    }

    [Fact]
    public void ToolDetailsPage_ShouldRenderCompactHeroSection_WithToolName()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [CreateTestToolCall()],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            ToolName = "RulesSearch",
            ToolDescription = "Search through game rules and mechanics"
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "RulesSearch"));

        // Assert - CompactHeroSection should render with tool name as title
        var heroSection = cut.FindComponent<CompactHeroSection>();
        heroSection.ShouldNotBeNull();
        heroSection.Instance.Title.ShouldBe("RulesSearch");
    }

    [Fact]
    public void ToolDetailsPage_ShouldHaveBreadcrumbs()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 10,
            ToolName = "TestTool",
            ToolDescription = null
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "TestTool"));

        // Assert - Breadcrumbs should be present
        var breadcrumbs = cut.FindComponent<MudBreadcrumbs>();
        breadcrumbs.ShouldNotBeNull();
    }

    [Fact]
    public void ToolDetailsPage_ActionTooltips_ShouldHavePlacementTop()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [CreateTestToolCall()],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            ToolName = "RulesSearch",
            ToolDescription = "Search through game rules"
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "RulesSearch"));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - All tooltips should have Placement.Top
        var tooltips = cut.FindComponents<MudTooltip>();
        foreach (var tooltip in tooltips)
        {
            tooltip.Instance.Placement.ShouldBe(Placement.Top, $"Tooltip with text '{tooltip.Instance.Text}' should have Placement.Top");
        }
    }

    [Fact]
    public void ToolDetailsPage_ShouldShowEmptyState_WhenNoToolCalls()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 10,
            ToolName = "RulesSearch",
            ToolDescription = "Search through game rules"
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "RulesSearch"));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Empty state message should be visible
        cut.Markup.ShouldContain("No calls recorded for this tool");
    }

    [Fact]
    public void ToolDetailsPage_ShouldShowToolCallDetails()
    {
        // Arrange
        var response = new ToolCallDetailListResponse
        {
            Items = [CreateTestToolCall(id: 42, toolName: "RulesSearch")],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            ToolName = "RulesSearch",
            ToolDescription = "Search through game rules"
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolDetails>(parameters => parameters
            .Add(p => p.ToolName, "RulesSearch"));
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Should show game name and agent info
        cut.Markup.ShouldContain("Test Game");
        cut.Markup.ShouldContain("Game Master");
    }
}
