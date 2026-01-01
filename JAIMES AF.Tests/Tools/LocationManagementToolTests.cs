using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MattEland.Jaimes.Tests.Tools;

public class LocationManagementToolTests
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
        Mock<IServiceProvider> mockServiceProvider = new();
        Should.Throw<ArgumentNullException>(() => new LocationManagementTool(null!, mockServiceProvider.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
    {
        GameDto game = CreateGameDto();
        Should.Throw<ArgumentNullException>(() => new LocationManagementTool(game, null!));
    }

    [Theory]
    [InlineData("", "Description", "Error: Location name is required")]
    [InlineData("   ", "Description", "Error: Location name is required")]
    [InlineData(null, "Description", "Error: Location name is required")]
    public async Task CreateOrUpdateLocationAsync_ReturnsError_WhenNameInvalid(string? name, string description,
        string expectedError)
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<IServiceProvider> mockServiceProvider = new();
        LocationManagementTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.CreateOrUpdateLocationAsync(name!, description);

        // Assert
        result.ShouldContain(expectedError);
    }

    [Fact]
    public async Task CreateOrUpdateLocationAsync_ReturnsError_WhenNameTooLong()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<IServiceProvider> mockServiceProvider = new();
        LocationManagementTool tool = new(game, mockServiceProvider.Object);
        string longName = new('a', 201);

        // Act
        string result = await tool.CreateOrUpdateLocationAsync(longName, "Description");

        // Assert
        result.ShouldContain("200 characters or less");
    }

    [Theory]
    [InlineData("Location", "", "Error: Location description is required")]
    [InlineData("Location", "   ", "Error: Location description is required")]
    [InlineData("Location", null, "Error: Location description is required")]
    public async Task CreateOrUpdateLocationAsync_ReturnsError_WhenDescriptionInvalid(string name, string? description,
        string expectedError)
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<IServiceProvider> mockServiceProvider = new();
        LocationManagementTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.CreateOrUpdateLocationAsync(name, description!);

        // Assert
        result.ShouldContain(expectedError);
    }

    [Fact]
    public async Task AddLocationEventAsync_ReturnsError_WhenLocationNotFound()
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

        LocationManagementTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.AddLocationEventAsync("NonExistent", "Event", "Event description");

        // Assert
        result.ShouldContain("was not found");
    }

    [Fact]
    public async Task AddNearbyLocationAsync_ReturnsError_WhenLinkingToSelf()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<IServiceProvider> mockServiceProvider = new();
        LocationManagementTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.AddNearbyLocationAsync("Village", "Village");

        // Assert
        result.ShouldContain("Cannot link a location to itself");
    }

    [Fact]
    public async Task AddNearbyLocationAsync_ReturnsError_WhenSourceNotFound()
    {
        // Arrange
        GameDto game = CreateGameDto();
        Mock<ILocationService> mockLocationService = new();
        mockLocationService.Setup(s =>
                s.GetLocationByNameAsync(It.IsAny<Guid>(), "NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LocationResponse?)null);
        mockLocationService.Setup(s =>
                s.GetLocationByNameAsync(It.IsAny<Guid>(), "Existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationResponse { Id = 1, Name = "Existing", Description = "Test" });

        Mock<IServiceScope> mockScope = new();
        mockScope.Setup(s => s.ServiceProvider.GetService(typeof(ILocationService)))
            .Returns(mockLocationService.Object);

        Mock<IServiceScopeFactory> mockScopeFactory = new();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        Mock<IServiceProvider> mockServiceProvider = new();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        LocationManagementTool tool = new(game, mockServiceProvider.Object);

        // Act
        string result = await tool.AddNearbyLocationAsync("NonExistent", "Existing");

        // Assert
        result.ShouldContain("'NonExistent' was not found");
    }
}
