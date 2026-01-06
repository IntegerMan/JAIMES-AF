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

public class ToolUsageTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public ToolUsageTests()
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

    private static ToolUsageItemDto CreateTestToolItem(string toolName = "TestTool", int totalCalls = 10)
    {
        return new ToolUsageItemDto
        {
            ToolName = toolName,
            TotalCalls = totalCalls,
            EligibleMessages = 100,
            UsagePercentage = 10.0,
            EnabledAgents = ["Agent1", "Agent2"],
            HelpfulCount = 5,
            UnhelpfulCount = 2
        };
    }

    private HttpClient CreateMockHttpClient(ToolUsageListResponse response)
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

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
    }

    private HttpClient CreateMockHttpClientFactory(ToolUsageListResponse response)
    {
        return CreateMockHttpClient(response);
    }

    private void SetupHttpClientFactory(ToolUsageListResponse response)
    {
        var httpClient = CreateMockHttpClient(response);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("Api")).Returns(httpClient);
        Services.AddSingleton(httpClientFactoryMock.Object);
    }

    [Fact]
    public void ToolUsagePage_ShouldRenderHeroSection()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem()],
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
        var cut = RenderComponent<ToolUsage>();

        // Assert - Hero section should be present with correct title
        cut.Markup.ShouldContain("Tool Usage Statistics");
        cut.Markup.ShouldContain("Test Tools"); // Action button text
    }

    [Fact]
    public void ToolUsagePage_ShouldShowToolCount_WhenToolsExist()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem("Tool1"), CreateTestToolItem("Tool2")],
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
        var cut = RenderComponent<ToolUsage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Should show "2 tools tracked"
        cut.Markup.ShouldContain("2 tools tracked");
    }

    [Fact]
    public void ToolUsagePage_ShouldShowSingularToolCount_WhenOneTool()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem()],
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
        var cut = RenderComponent<ToolUsage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Should show "1 tool tracked" (singular)
        cut.Markup.ShouldContain("1 tool tracked");
        cut.Markup.ShouldNotContain("1 tools"); // Should not have incorrect plural
    }

    [Fact]
    public void ToolUsagePage_ShouldShowStatsCards_WhenDataLoaded()
    {
        // Arrange
        var response = new ToolUsageListResponse
        {
            Items = [CreateTestToolItem()],
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalCalls = 150,
            TotalHelpful = 25,
            TotalUnhelpful = 5,
            AverageUsagePercentage = 35.5
        };
        SetupHttpClientFactory(response);
        SetupMudProviders();

        // Act
        var cut = RenderComponent<ToolUsage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0, TimeSpan.FromSeconds(5));

        // Assert - Stats cards should be visible
        cut.Markup.ShouldContain("Total Calls");
        cut.Markup.ShouldContain("150");
        cut.Markup.ShouldContain("Helpful");
        cut.Markup.ShouldContain("25");
        cut.Markup.ShouldContain("Unhelpful");
        cut.Markup.ShouldContain("Avg Usage");
        cut.Markup.ShouldContain("35.5%");
    }

    [Fact]
    public void ToolUsagePage_ShouldHaveBreadcrumbs()
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
        var cut = RenderComponent<ToolUsage>();

        // Assert - Breadcrumbs should be present
        var breadcrumbs = cut.FindComponent<MudBreadcrumbs>();
        breadcrumbs.ShouldNotBeNull();
    }
}
