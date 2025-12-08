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
        Ruleset? ruleset = await context.Rulesets.FindAsync(["dnd5e"], ct);
        ruleset.ShouldNotBeNull();
        ruleset.Name.ShouldBe("Dungeons and Dragons 5th Edition");

        Player? player = await context.Players.FindAsync(["emcee"], ct);
        player.ShouldNotBeNull();
        player.Name.ShouldBe("Emcee");
        player.RulesetId.ShouldBe("dnd5e");

        Scenario? scenario = await context.Scenarios.FindAsync(["islandTest"], ct);
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
        Ruleset? ruleset = await context.Rulesets.FindAsync(["dnd5e"], ct);
        ruleset.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeDatabaseAsync_LogsProviderInformation()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());

        List<string> logMessages = new();
        services.AddLogging(builder => { builder.AddProvider(new TestLoggerProvider(logMessages)); });

        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        await provider.InitializeDatabaseAsync();

        // Assert
        logMessages.ShouldContain(msg => msg.Contains("Starting database initialization"));
        logMessages.ShouldContain(msg => msg.Contains("Microsoft.EntityFrameworkCore.InMemory"));
    }

    private class TestLoggerProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(messages);
        }

        public void Dispose()
        {
        }
    }

    private class TestLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }
}