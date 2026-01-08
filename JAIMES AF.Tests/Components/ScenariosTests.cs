using Bunit;
using MattEland.Jaimes.Web.Components.Pages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using Shouldly;
using Xunit;
using MudBlazor;
using Moq.Protected;
using System.Net.Http.Json;

namespace MattEland.Jaimes.Tests.Components;

public class ScenariosTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public ScenariosTests()
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

    private static ScenarioInfoResponse CreateTestScenario(string name = "Test Scenario",
        string description = "A test scenario")
    {
        return new ScenarioInfoResponse
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            RulesetId = "dnd5e"
        };
    }

    private HttpClient CreateMockHttpClient(ScenarioInfoResponse[] scenarios)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = new ScenarioListResponse { Scenarios = scenarios };

        // Setup GET /scenarios
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery == "/scenarios"),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = JsonContent.Create(response),
            });

        // Setup GET /scenarios/{id}/agents for each scenario
        foreach (var scenario in scenarios)
        {
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(m =>
                        m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery == $"/scenarios/{scenario.Id}/agents"),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = JsonContent.Create(new ScenarioAgentListResponse { ScenarioAgents = [] }),
                });
        }

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public void ScenariosPage_ShouldShowScenarioCount_WhenScenariosExist()
    {
        // Arrange
        var scenarios = new[]
        {
            CreateTestScenario("Scenario 1"),
            CreateTestScenario("Scenario 2"),
            CreateTestScenario("Scenario 3")
        };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("3 scenarios available");
    }

    [Fact]
    public void ScenariosPage_ShouldShowSingularCount_WhenOneScenarioExists()
    {
        // Arrange
        var scenarios = new[] { CreateTestScenario() };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("1 scenario available");
        cut.Markup.ShouldNotContain("1 scenarios available");
    }

    [Fact]
    public void ScenariosPage_ShouldShowEmptyState_WhenNoScenarios()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No scenarios yet");
        cut.Markup.ShouldContain("Create Your First Scenario");
        cut.Markup.ShouldContain("story seeds");
    }

    [Fact]
    public void ScenariosPage_ActionButtons_ShouldHaveTooltipsWithPlacementTop()
    {
        // Arrange
        var scenarios = new[] { CreateTestScenario() };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
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
    public void ScenariosPage_ShouldRenderNewScenarioButton()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("New Scenario");
    }

    [Fact]
    public void ScenariosPage_ShouldShowHeroSection_WithCorrectTitle()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("Your Scenarios");
    }

    [Fact]
    public void ScenariosPage_ShouldHaveEditButton_WithTooltip()
    {
        // Arrange
        var scenarios = new[] { CreateTestScenario() };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find edit button
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit);
        editButtons.ShouldNotBeEmpty("Edit button should exist");
    }

    [Fact]
    public void ScenariosPage_ScenarioNameLink_ShouldNavigateToEditPage()
    {
        // Arrange
        var scenario = CreateTestScenario("My Adventure");
        var scenarios = new[] { scenario };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify scenario name is a link to edit page
        var links = cut.FindComponents<MudLink>();
        var scenarioLink = links.FirstOrDefault(l => l.Markup.Contains("My Adventure"));
        scenarioLink.ShouldNotBeNull();
        (scenarioLink.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void ScenariosPage_EditButton_ShouldLinkToEditPage()
    {
        // Arrange
        var scenario = CreateTestScenario();
        var scenarios = new[] { scenario };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify edit button links to edit page
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit)
            .ToList();
        editButtons.ShouldNotBeEmpty();

        var editButton = editButtons.First();
        (editButton.Instance.Href ?? "").ShouldContain(scenario.Id);
        (editButton.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void ScenariosPage_ShouldDisplayScenarioDescription()
    {
        // Arrange
        var scenario = CreateTestScenario("Test Scenario", "A whimsical fantasy adventure");
        var scenarios = new[] { scenario };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("A whimsical fantasy adventure");
    }

    [Fact]
    public void ScenariosPage_ShouldShowRulesetId_InTable()
    {
        // Arrange
        var scenario = CreateTestScenario();
        var scenarios = new[] { scenario };
        Services.AddSingleton(CreateMockHttpClient(scenarios));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Scenarios>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - RulesetLink component should render with the ruleset ID
        cut.Markup.ShouldContain("dnd5e");
    }
}
