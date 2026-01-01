using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MattEland.Jaimes.Tests.Tools;

public class GameLocationToolTests
{
    private static GameDto CreateGameDto()
    {
        return new GameDto
        {
            GameId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Player = new PlayerDto { Id = "player-1", Name = "Test Player", RulesetId = "ruleset-1" },
            Scenario = new ScenarioDto { Id = "scenario-1", Name = "Test Scenario", RulesetId = "ruleset-1" },
            Ruleset = new RulesetDto { Id = "ruleset-1", Name = "Test Ruleset" }
        };
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenGameIsNull()
    {
        // Arrange
        Mock<IServiceProvider> mockServiceProvider = new();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new GameLocationTool(null!, mockServiceProvider.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
    {
        // Arrange
        GameDto game = CreateGameDto();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new GameLocationTool(game, null!));
    }

    [Fact]
    public async Task GetLocationByNameAsync_ReturnsError_WhenNameIsEmpty()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<IServiceProvider> mockServiceProvider = new();
        GameLocationTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.GetLocationByNameAsync("");

        // Assert
        result.ShouldContain("Please provide a location name");
    }

    [Fact]
    public async Task GetLocationByNameAsync_ReturnsNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<ILocationService> mockLocationService = new();
        mockLocationService.Setup(s =>
                s.GetLocationByNameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LocationResponse?)null);

        Mock<IServiceScope> mockScope = new();
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(ILocationService)))
            .Returns(mockLocationService.Object);

        Mock<IServiceScopeFactory> mockScopeFactory = new();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        Mock<IServiceProvider> mockServiceProvider = new();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        GameLocationTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.GetLocationByNameAsync("NonExistentLocation");

        // Assert
        result.ShouldContain("was not found");
    }

    [Fact]
    public async Task GetAllLocationsAsync_ReturnsEmptyMessage_WhenNoLocationsExist()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<ILocationService> mockLocationService = new();
        mockLocationService.Setup(s => s.GetLocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationListResponse { Locations = [], TotalCount = 0 });

        Mock<IServiceScope> mockScope = new();
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(ILocationService)))
            .Returns(mockLocationService.Object);

        Mock<IServiceScopeFactory> mockScopeFactory = new();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        Mock<IServiceProvider> mockServiceProvider = new();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        GameLocationTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.GetAllLocationsAsync();

        // Assert
        result.ShouldContain("No locations have been established");
    }

    [Fact]
    public async Task GetAllLocationsAsync_ReturnsFormattedList_WhenLocationsExist()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<ILocationService> mockLocationService = new();
        mockLocationService.Setup(s => s.GetLocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationListResponse
            {
                Locations =
                [
                    new LocationResponse
                    {
                        Id = 1, Name = "The Village", Description = "A quiet village", EventCount = 2
                    },
                    new LocationResponse
                    {
                        Id = 2, Name = "The Forest", Description = "A dark forest", EventCount = 0
                    }
                ],
                TotalCount = 2
            });

        Mock<IServiceScope> mockScope = new();
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(ILocationService)))
            .Returns(mockLocationService.Object);

        Mock<IServiceScopeFactory> mockScopeFactory = new();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        Mock<IServiceProvider> mockServiceProvider = new();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        GameLocationTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.GetAllLocationsAsync();

        // Assert
        result.ShouldContain("Known locations (2)");
        result.ShouldContain("The Village");
        result.ShouldContain("The Forest");
        result.ShouldContain("(2 event(s) recorded)");
    }
}
