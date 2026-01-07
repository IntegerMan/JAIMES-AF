using Bunit;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Moq.Protected;
using System.Net.Http.Json;

// Alias to avoid collision with MattEland.Jaimes.Tests.Agents namespace
using AgentsPage = MattEland.Jaimes.Web.Components.Pages.Agents;

namespace MattEland.Jaimes.Tests.Components;

public class AgentsTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    public AgentsTests()
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

    private static AgentResponse CreateTestAgent(string name = "Test Agent", string role = "GameMaster")
    {
        return new AgentResponse
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Role = role
        };
    }

    private HttpClient CreateMockHttpClient(AgentResponse[] agents)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = new AgentListResponse { Agents = agents };
        var statsResponse = new AgentStatsResponse();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/agents") &&
                    !m.RequestUri.PathAndQuery.Contains("/stats")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(response),
            });

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/stats")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(statsResponse),
            });

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public void AgentsPage_ShouldShowAgentCount_WhenAgentsExist()
    {
        // Arrange
        var agents = new[]
        {
            CreateTestAgent("Agent 1"),
            CreateTestAgent("Agent 2"),
            CreateTestAgent("Agent 3")
        };
        Services.AddSingleton(CreateMockHttpClient(agents));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("3 agents available");
    }

    [Fact]
    public void AgentsPage_ShouldShowSingularCount_WhenOneAgentExists()
    {
        // Arrange
        var agents = new[] { CreateTestAgent() };
        Services.AddSingleton(CreateMockHttpClient(agents));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("1 agent available");
        cut.Markup.ShouldNotContain("1 agents available");
    }

    [Fact]
    public void AgentsPage_ShouldShowEmptyState_WhenNoAgents()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No agents yet");
        cut.Markup.ShouldContain("Create Your First Agent");
    }

    [Fact]
    public void AgentsPage_ActionButtons_ShouldHaveTooltipsWithPlacementTop()
    {
        // Arrange
        var agents = new[] { CreateTestAgent() };
        Services.AddSingleton(CreateMockHttpClient(agents));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find all MudTooltip components
        var tooltips = cut.FindComponents<MudTooltip>();
        tooltips.ShouldNotBeEmpty("Action buttons should be wrapped in tooltips");

        // All tooltips should have Placement.Top
        foreach (var tooltip in tooltips)
        {
            tooltip.Instance.Placement.ShouldBe(Placement.Top, "All tooltips should have Placement.Top");
        }
    }

    [Fact]
    public void AgentsPage_ShouldRenderNewAgentButton()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("New Agent");
    }

    [Fact]
    public void AgentsPage_ShouldShowHeroSection_WithCorrectTitle()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("AI Agents");
    }

    [Fact]
    public void AgentsPage_EditButton_ShouldLinkToEditPage()
    {
        // Arrange
        var agent = CreateTestAgent();
        var agents = new[] { agent };
        Services.AddSingleton(CreateMockHttpClient(agents));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<AgentsPage>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify edit button links to edit page
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit)
            .ToList();
        editButtons.ShouldNotBeEmpty();

        var editButton = editButtons.First();
        (editButton.Instance.Href ?? "").ShouldContain(agent.Id);
        (editButton.Instance.Href ?? "").ShouldContain("/edit");
    }
}
