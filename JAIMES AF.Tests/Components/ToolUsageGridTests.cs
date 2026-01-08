using Bunit;
using MattEland.Jaimes.Web.Components.Shared;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.AspNetCore.Components;
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

public class ToolUsageGridTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public ToolUsageGridTests()
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

    private static ToolUsageItemDto CreateTestToolItem(
        string toolName = "TestTool", 
        int totalCalls = 10,
        int helpfulCount = 5,
        int unhelpfulCount = 2,
        double usagePercentage = 10.0)
    {
        return new ToolUsageItemDto
        {
            ToolName = toolName,
            TotalCalls = totalCalls,
            EligibleMessages = 100,
            UsagePercentage = usagePercentage,
            EnabledAgents = ["Agent1", "Agent2"],
            HelpfulCount = helpfulCount,
            UnhelpfulCount = unhelpfulCount
        };
    }

    private void SetupHttpClientFactory(ToolUsageListResponse response)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/admin/tools")),
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
    public void ToolUsageGrid_ShouldRenderToolNames()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem("RulesSearch"), CreateTestToolItem("CreateLocation")],
            TotalCount = 2,
            Page = 1,
            PageSize = 10,
            TotalCalls = 20,
            TotalHelpful = 10,
            TotalUnhelpful = 4,
            AverageUsagePercentage = 15.0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("RulesSearch");
        cut.Markup.ShouldContain("CreateLocation");
    }

    [Fact]
    public void ToolUsageGrid_ToolNamesShouldBeLinks()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem("RulesSearch")],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 10,
            TotalHelpful = 5,
            TotalUnhelpful = 2,
            AverageUsagePercentage = 10.0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Tool name should be rendered as a link
        var links = cut.FindComponents<MudLink>();
        links.ShouldNotBeEmpty("Tool names should be rendered as links");
        
        // Find the tool name link
        var toolLink = links.FirstOrDefault(l => l.Markup.Contains("RulesSearch"));
        toolLink.ShouldNotBeNull("RulesSearch should be a link");
        toolLink!.Instance.Href.ShouldNotBeNull();
        toolLink.Instance.Href!.ShouldContain("/admin/tools/RulesSearch");
    }

    [Fact]
    public void ToolUsageGrid_ShouldShowEmptyState_WhenNoTools()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 10,
            TotalCalls = 0,
            TotalHelpful = 0,
            TotalUnhelpful = 0,
            AverageUsagePercentage = 0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Empty state message should be visible
        cut.Markup.ShouldContain("No Tool Usage Data");
        cut.Markup.ShouldContain("No tool calls have been recorded yet");
        cut.Markup.ShouldContain("Test Tools"); // CTA button
    }

    [Fact]
    public void ToolUsageGrid_ShouldShowHelpfulAndUnhelpfulCounts()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem("TestTool", totalCalls: 100, helpfulCount: 25, unhelpfulCount: 5)],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 100,
            TotalHelpful = 25,
            TotalUnhelpful = 5,
            AverageUsagePercentage = 50.0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("25"); // Helpful count
        cut.Markup.ShouldContain("5");  // Unhelpful count
    }

    [Fact]
    public void ToolUsageGrid_ShouldShowUsagePercentage()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem("TestTool", usagePercentage: 45.5)],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 100,
            TotalHelpful = 25,
            TotalUnhelpful = 5,
            AverageUsagePercentage = 45.5
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert
        cut.Markup.ShouldContain("45.5%");
    }

    [Fact]
    public void ToolUsageGrid_ShouldShowEnabledAgents()
    {
        // Arrange
        var item = CreateTestToolItem("TestTool");
        var response = new ToolUsageListResponse
        {
            Items = [item],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 10,
            TotalHelpful = 5,
            TotalUnhelpful = 2,
            AverageUsagePercentage = 10.0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsageGrid>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Should show agent names
        cut.Markup.ShouldContain("Agent1");
        cut.Markup.ShouldContain("Agent2");
    }

    [Fact]
    public void ToolUsageGrid_ShouldInvokeOnStatsLoaded_WhenDataLoaded()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem()],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 100,
            TotalHelpful = 25,
            TotalUnhelpful = 5,
            AverageUsagePercentage = 35.0
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        ToolUsageListResponse? receivedStats = null;

        // Act
        var cut = RenderComponent<ToolUsageGrid>(parameters => parameters
            .Add(p => p.OnStatsLoaded, EventCallback.Factory.Create<ToolUsageListResponse>(this, stats => receivedStats = stats)));
        
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Callback should have been invoked with stats
        receivedStats.ShouldNotBeNull();
        receivedStats.TotalCalls.ShouldBe(100);
        receivedStats.TotalHelpful.ShouldBe(25);
        receivedStats.TotalUnhelpful.ShouldBe(5);
        receivedStats.AverageUsagePercentage.ShouldBe(35.0);
    }
}
