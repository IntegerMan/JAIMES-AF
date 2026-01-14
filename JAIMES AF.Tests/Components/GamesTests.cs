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

public class GamesTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public GamesTests()
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

    private static GameInfoResponse CreateTestGame(string title = "Test Game", bool useLatestVersion = false)
    {
        return new GameInfoResponse
        {
            GameId = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow,
            ScenarioId = "scenario-123",
            ScenarioName = "Test Scenario",
            RulesetId = "ruleset-123",
            RulesetName = "Test Ruleset",
            PlayerId = "player-123",
            PlayerName = "Test Player",
            AgentId = "agent-123",
            AgentName = "Game Master",
            InstructionVersionId = useLatestVersion ? null : 1,
            VersionNumber = useLatestVersion ? null : "v1.0"
        };
    }

    private HttpClient CreateMockHttpClient(GameInfoResponse[] games)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = new ListGamesResponse { Games = games };

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
                Content = JsonContent.Create(response),
            });

        return new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
    }

    [Fact]
    public void GamesPage_ShouldShowGameCount_WhenGamesExist()
    {
        // Arrange
        var games = new[]
        {
            CreateTestGame("Test Game 1"),
            CreateTestGame("Test Game 2")
        };
        Services.AddSingleton(CreateMockHttpClient(games));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("2 games in progress");
    }

    [Fact]
    public void GamesPage_ShouldShowEmptyState_WhenNoGames()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No games yet");
        cut.Markup.ShouldContain("Start Your Adventure");
    }

    [Fact]
    public void GamesPage_ActionButtons_ShouldHaveTooltipsWithPlacementTop()
    {
        // Arrange
        var games = new[] { CreateTestGame() };
        Services.AddSingleton(CreateMockHttpClient(games));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
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
    public void GamesPage_ShouldShowLatestChip_WhenGameUsesLatestVersion()
    {
        // Arrange - Create a game that uses the latest version (no specific version set)
        var games = new[] { CreateTestGame(useLatestVersion: true) };
        Services.AddSingleton(CreateMockHttpClient(games));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Should find the "Latest" text
        cut.Markup.ShouldContain("Latest");
    }

    [Fact]
    public void GamesPage_ShouldShowVersionNumber_WhenGameHasSpecificVersion()
    {
        // Arrange - Create a game with a specific version
        var games = new[] { CreateTestGame(useLatestVersion: false) };
        Services.AddSingleton(CreateMockHttpClient(games));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Should find the version number
        cut.Markup.ShouldContain("v1.0");
        cut.Markup.ShouldNotContain(">Latest<"); // Should not show Latest chip
    }

    [Fact]
    public void GamesPage_ShouldRenderNewGameButton()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("New Game");
    }

    [Fact]
    public async Task GamesPage_DeleteButton_ShouldShowConfirmationDialog()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var games = new[]
        {
            new GameInfoResponse
            {
                GameId = gameId,
                Title = "Game to Delete",
                CreatedAt = DateTime.UtcNow,
                ScenarioId = "scenario-123",
                ScenarioName = "Test Scenario",
                RulesetId = "ruleset-123",
                RulesetName = "Test Ruleset",
                PlayerId = "player-123",
                PlayerName = "Test Player",
                AgentId = "agent-123",
                AgentName = "Game Master"
            }
        };

        // Setup mock to return null (cancel) from the dialog
        _dialogServiceMock
            .Setup(x => x.ShowMessageBoxAsync(
                It.Is<string>(s => s == "Delete Game"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync((bool?)null);

        Services.AddSingleton(CreateMockHttpClient(games));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Find and click the delete button
        var deleteButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Delete);
        deleteButtons.ShouldNotBeEmpty("Delete button should exist");

        await cut.InvokeAsync(() => deleteButtons.First().Instance.OnClick.InvokeAsync());

        // Assert - Verify the confirmation dialog was shown
        _dialogServiceMock.Verify(x => x.ShowMessageBoxAsync(
            "Delete Game",
            It.IsAny<string>(),
            "Delete",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DialogOptions>()), Times.Once);
    }

    [Fact]
    public async Task GamesPage_DeleteButton_ShouldRemoveGameFromList_WhenConfirmed()
    {
        // Arrange
        var gameToDeleteId = Guid.NewGuid();
        var gameToKeepId = Guid.NewGuid();

        var initialGames = new[]
        {
            new GameInfoResponse
            {
                GameId = gameToDeleteId,
                Title = "Game to Delete",
                CreatedAt = DateTime.UtcNow,
                ScenarioId = "scenario-123",
                ScenarioName = "Test Scenario",
                RulesetId = "ruleset-123",
                RulesetName = "Test Ruleset",
                PlayerId = "player-123",
                PlayerName = "Test Player",
                AgentId = "agent-123",
                AgentName = "Game Master"
            },
            new GameInfoResponse
            {
                GameId = gameToKeepId,
                Title = "Game to Keep",
                CreatedAt = DateTime.UtcNow,
                ScenarioId = "scenario-456",
                ScenarioName = "Another Scenario",
                RulesetId = "ruleset-456",
                RulesetName = "Another Ruleset",
                PlayerId = "player-456",
                PlayerName = "Another Player",
                AgentId = "agent-456",
                AgentName = "Another Agent"
            }
        };

        var gamesAfterDelete = new[] { initialGames[1] }; // Only the second game remains

        // Track call count to return different results
        var getCallCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // Setup GET /games - returns different results based on call count
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/games")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                getCallCount++;
                var games = getCallCount == 1 ? initialGames : gamesAfterDelete;
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = JsonContent.Create(new ListGamesResponse { Games = games }),
                };
            });

        // Setup DELETE /games/{id}
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Delete && m.RequestUri!.PathAndQuery.Contains($"/games/{gameToDeleteId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(httpClient);

        // Setup mock dialog to confirm deletion (return true)
        _dialogServiceMock
            .Setup(x => x.ShowMessageBoxAsync(
                It.Is<string>(s => s == "Delete Game"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(true);

        SetupMudProviders();

        // Act
        var cut = RenderComponent<Games>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Verify both games are initially shown
        cut.Markup.ShouldContain("Game to Delete");
        cut.Markup.ShouldContain("Game to Keep");
        cut.Markup.ShouldContain("2 games in progress");

        // Find and click the first delete button (for "Game to Delete")
        var deleteButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Delete)
            .ToList();
        deleteButtons.Count.ShouldBe(2, "Should have 2 delete buttons initially");

        await cut.InvokeAsync(() => deleteButtons.First().Instance.OnClick.InvokeAsync());

        // Wait for the component to re-render after the delete
        cut.WaitForState(() => !cut.Markup.Contains("Game to Delete"), TimeSpan.FromSeconds(5));

        // Assert
        // Verify DELETE endpoint was called
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(m =>
                m.Method == HttpMethod.Delete && m.RequestUri!.PathAndQuery.Contains($"/games/{gameToDeleteId}")),
            ItExpr.IsAny<CancellationToken>());

        // Verify the deleted game is no longer in the list
        cut.Markup.ShouldNotContain("Game to Delete");
        cut.Markup.ShouldContain("Game to Keep");
        cut.Markup.ShouldContain("1 game in progress"); // Count updated
    }
}
