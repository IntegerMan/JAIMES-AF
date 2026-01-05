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

public class PlayersTests : Bunit.TestContext
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    public PlayersTests()
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

    private static PlayerInfoResponse CreateTestPlayer(string name = "Test Player",
        string description = "A test player character",
        string rulesetId = "dnd5e")
    {
        return new PlayerInfoResponse
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            RulesetId = rulesetId
        };
    }

    private HttpClient CreateMockHttpClient(PlayerInfoResponse[] players)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var response = new PlayerListResponse { Players = players };

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(m =>
                    m.Method == HttpMethod.Get && m.RequestUri!.PathAndQuery.Contains("/players")),
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
    public void PlayersPage_ShouldShowPlayerCount_WhenPlayersExist()
    {
        // Arrange
        var players = new[]
        {
            CreateTestPlayer("Player 1"),
            CreateTestPlayer("Player 2"),
            CreateTestPlayer("Player 3")
        };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("3 players available");
    }

    [Fact]
    public void PlayersPage_ShouldShowSingularCount_WhenOnePlayerExists()
    {
        // Arrange
        var players = new[] { CreateTestPlayer() };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("1 player available");
        cut.Markup.ShouldNotContain("1 players available");
    }

    [Fact]
    public void PlayersPage_ShouldShowEmptyState_WhenNoPlayers()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No players yet");
        cut.Markup.ShouldContain("Create Your First Player");
    }

    [Fact]
    public void PlayersPage_ActionButtons_ShouldHaveTooltipsWithPlacementTop()
    {
        // Arrange
        var players = new[] { CreateTestPlayer() };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
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
    public void PlayersPage_ShouldRenderNewPlayerButton()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("New Player");
    }

    [Fact]
    public void PlayersPage_ShouldShowHeroSection_WithCorrectTitle()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("Your Players");
    }

    [Fact]
    public void PlayersPage_ShouldHaveEditButton_WithTooltip()
    {
        // Arrange
        var players = new[] { CreateTestPlayer() };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Find edit button
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit);
        editButtons.ShouldNotBeEmpty("Edit button should exist");
    }

    [Fact]
    public void PlayersPage_PlayerNameLink_ShouldNavigateToEditPage()
    {
        // Arrange
        var player = CreateTestPlayer("Glim the Frog Wizard");
        var players = new[] { player };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify player name is a link to edit page
        var links = cut.FindComponents<MudLink>();
        var playerLink = links.FirstOrDefault(l => l.Markup.Contains("Glim the Frog Wizard"));
        playerLink.ShouldNotBeNull();
        (playerLink.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void PlayersPage_EditButton_ShouldLinkToEditPage()
    {
        // Arrange
        var player = CreateTestPlayer();
        var players = new[] { player };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Verify edit button links to edit page
        var editButtons = cut.FindComponents<MudIconButton>()
            .Where(b => b.Instance.Icon == Icons.Material.Filled.Edit)
            .ToList();
        editButtons.ShouldNotBeEmpty();

        var editButton = editButtons.First();
        (editButton.Instance.Href ?? "").ShouldContain(player.Id);
        (editButton.Instance.Href ?? "").ShouldContain("/edit");
    }

    [Fact]
    public void PlayersPage_ShouldDisplayPlayerDescription()
    {
        // Arrange
        var player = CreateTestPlayer("Test Player", "A brave adventurer with a heart of gold");
        var players = new[] { player };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert
        cut.Markup.ShouldContain("A brave adventurer with a heart of gold");
    }

    [Fact]
    public void PlayersPage_ShouldShowRulesetId_InTable()
    {
        // Arrange
        var player = CreateTestPlayer(rulesetId: "dnd5e");
        var players = new[] { player };
        Services.AddSingleton(CreateMockHttpClient(players));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - RulesetLink component should render with the ruleset ID
        cut.Markup.ShouldContain("dnd5e");
    }

    [Fact]
    public void PlayersPage_ShouldShowCreateMessage_WhenNoPlayers()
    {
        // Arrange
        Services.AddSingleton(CreateMockHttpClient([]));
        SetupMudProviders();

        // Act
        var cut = RenderComponent<Players>();
        cut.WaitForState(() => cut.FindAll(".mud-progress-circular").Count == 0);

        // Assert - Hero section should show encouraging message
        cut.Markup.ShouldContain("Create your first player");
    }
}
