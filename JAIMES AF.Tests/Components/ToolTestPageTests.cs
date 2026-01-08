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

public class ToolTestPageTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    public ToolTestPageTests()
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

    private void SetupHttpClientFactory(ToolMetadataResponse[] tools, GameInfoResponse[] games)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // Setup tools endpoint
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/admin/tools/available")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(new ToolMetadataListResponse { Tools = tools }),
            });

        // Setup games endpoint
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/games")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(new ListGamesResponse { Games = games }),
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Api")).Returns(httpClient);
        Services.AddSingleton(httpClientFactoryMock.Object);
    }

    [Fact]
    public void ToolTestPage_ShouldRenderHeroSection()
    {
        // Arrange
        SetupHttpClientFactory([], []);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolTestPage>();

        // Assert - Hero section should be present with correct title
        cut.Markup.ShouldContain("Tool Testing");
        cut.Markup.ShouldContain("View Tool Usage"); // Action button text
    }

    [Fact]
    public void ToolTestPage_ShouldHaveBreadcrumbs()
    {
        // Arrange
        SetupHttpClientFactory([], []);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolTestPage>();

        // Assert - Breadcrumbs should be present
        var breadcrumbs = cut.FindComponent<MudBreadcrumbs>();
        breadcrumbs.ShouldNotBeNull();
    }

    [Fact]
    public void ToolTestPage_ShouldShowToolSelectionSection()
    {
        // Arrange
        SetupHttpClientFactory([], []);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolTestPage>();

        // Assert - Tool selection section should be visible
        cut.Markup.ShouldContain("Tool Selection");
        cut.Markup.ShouldContain("Select a tool to test");
    }

    [Fact]
    public void ToolTestPage_ShouldShowInstructions()
    {
        // Arrange
        SetupHttpClientFactory([], []);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolTestPage>();

        // Assert - Instructions should be visible
        cut.Markup.ShouldContain("Select a tool to test");
        cut.Markup.ShouldContain("Tool executions from this page are not logged for diagnostics");
    }

    [Fact]
    public void ToolTestPage_ShouldHaveToolSelector()
    {
        // Arrange
        var tools = new[]
        {
            new ToolMetadataResponse
            {
                Name = "RulesSearch",
                Description = "Search game rules",
                Category = "Game",
                RequiresGameContext = true,
                Parameters = [],
                ClassName = "RulesSearchTool",
                MethodName = "SearchAsync"
            }
        };
        SetupHttpClientFactory(tools, []);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolTestPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Tool selector should be present
        var selects = cut.FindComponents<MudSelect<string>>();
        selects.ShouldNotBeEmpty("Tool selector should be present");
    }
}
