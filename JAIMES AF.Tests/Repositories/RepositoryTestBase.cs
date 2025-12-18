namespace MattEland.Jaimes.Tests.Repositories;

public abstract class RepositoryTestBase : IAsyncLifetime
{
    protected JaimesDbContext Context = null!;

    public virtual async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Context = new JaimesDbContext(options);
        await Context.Database.EnsureCreatedAsync(ct);

        // Add test data for validation
        Context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        Context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified" });
        Context.Scenarios.Add(new Scenario
        {
            Id = "test-scenario",
            RulesetId = "test-ruleset",
            Name = "Unspecified"
        });
        await Context.SaveChangesAsync(ct);
        Context.ChangeTracker.Clear();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
    }
}