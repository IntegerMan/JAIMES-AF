using MattEland.Jaimes.ApiService;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MattEland.Jaimes.Tests.Endpoints;

public abstract class EndpointTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<Program> Factory = null!;
    protected HttpClient Client = null!;

    public virtual async ValueTask InitializeAsync()
    {
        // Use a unique database name per test instance
        string dbName = $"TestDb_{Guid.NewGuid()}";
        CancellationToken ct = TestContext.Current.CancellationToken;
        
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Override settings for testing - use environment variable style which takes precedence
                builder.UseSetting("DatabaseProvider", "InMemory");
                builder.UseSetting("ConnectionStrings:DefaultConnection", dbName);
                builder.UseSetting("SkipDatabaseInitialization", "true");
            });

        Client = Factory.CreateClient();

        // Seed test data after initialization
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            await SeedTestDataAsync(context, ct);
        }
    }

    protected virtual async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        // Add default test data
        context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset", Name = "Unspecified" });
        context.Scenarios.Add(new Scenario { Id = "test-scenario", RulesetId = "test-ruleset", Name = "Unspecified", SystemPrompt = "UPDATE ME", NewGameInstructions = "UPDATE ME" });
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await Factory.DisposeAsync();
    }
}
