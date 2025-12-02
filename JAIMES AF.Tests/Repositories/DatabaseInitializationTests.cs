using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class DatabaseInitializationTests
{
    [Fact]
    public async Task InitializeDatabaseAsync_WithInMemoryProvider_CreatesDatabase()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());
        services.AddLogging();
        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        await provider.InitializeDatabaseAsync();

        // Assert
        using IServiceScope scope = provider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        
        // Verify seed data was created
        Entities.Ruleset? ruleset = await context.Rulesets.FindAsync(["dnd5e"], ct);
        ruleset.ShouldNotBeNull();
        ruleset.Name.ShouldBe("Dungeons and Dragons 5th Edition");

        Entities.Player? player = await context.Players.FindAsync(["emcee"], ct);
        player.ShouldNotBeNull();
        player.Name.ShouldBe("Emcee");
        player.RulesetId.ShouldBe("dnd5e");

        Entities.Scenario? scenario = await context.Scenarios.FindAsync(["islandTest"], ct);
        scenario.ShouldNotBeNull();
        scenario.Name.ShouldBe("Island Test");
        scenario.RulesetId.ShouldBe("dnd5e");
        scenario.SystemPrompt.ShouldContain("Dungeon Master");
        scenario.NewGameInstructions.ShouldContain("tropical beach");
    }

    [Fact]
    public async Task InitializeDatabaseAsync_CalledMultipleTimes_IsIdempotent()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());
        services.AddLogging();
        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        await provider.InitializeDatabaseAsync();
        await provider.InitializeDatabaseAsync();
        await provider.InitializeDatabaseAsync();

        // Assert - should not throw and database should still be valid
        using IServiceScope scope = provider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        
        int rulesetCount = await context.Rulesets.CountAsync(ct);
        rulesetCount.ShouldBe(1); // Should still only have one ruleset
    }

    [Fact]
    public async Task InitializeDatabaseAsync_WithoutLogger_StillWorks()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());
        // Intentionally not adding logging
        ServiceProvider provider = services.BuildServiceProvider();

        // Act & Assert - should not throw
        await provider.InitializeDatabaseAsync();

        using IServiceScope scope = provider.CreateScope();
        JaimesDbContext context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
        Entities.Ruleset? ruleset = await context.Rulesets.FindAsync(["dnd5e"], ct);
        ruleset.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeDatabaseAsync_LogsProviderInformation()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());
        
        List<string> logMessages = new();
        services.AddLogging(builder =>
        {
            builder.AddProvider(new TestLoggerProvider(logMessages));
        });
        
        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        await provider.InitializeDatabaseAsync();

        // Assert
        logMessages.ShouldContain(msg => msg.Contains("Starting database initialization"));
        logMessages.ShouldContain(msg => msg.Contains("Microsoft.EntityFrameworkCore.InMemory"));
    }

    private class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages;

        public TestLoggerProvider(List<string> messages)
        {
            _messages = messages;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_messages);
        }

        public void Dispose()
        {
        }
    }

    private class TestLogger : ILogger
    {
        private readonly List<string> _messages;

        public TestLogger(List<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
        }
    }
}
