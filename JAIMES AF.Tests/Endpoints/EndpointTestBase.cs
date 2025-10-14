using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tests.Endpoints;

public abstract class EndpointTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<Program> Factory = null!;
    protected HttpClient Client = null!;

    public virtual async ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Override configuration for testing
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    // Add our test configuration with highest priority
                    var inMemorySettings = new Dictionary<string, string?>
                    {
                        ["DatabaseProvider"] = "InMemory",
                        ["ConnectionStrings:DefaultConnection"] = string.Empty,
                        ["SkipDatabaseInitialization"] = "true"
                    };

                    configBuilder.AddInMemoryCollection(inMemorySettings);
                });

                // Replace the DbContext registration to ensure InMemory is used
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registration
                    var contextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(JaimesDbContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }

                    var optionsDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<JaimesDbContext>));
                    if (optionsDescriptor != null)
                    {
                        services.Remove(optionsDescriptor);
                    }

                    // Add InMemory DbContext
                    services.AddDbContext<JaimesDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                    });
                });
            });

        Client = Factory.CreateClient();

        // Seed test data after initialization
        using (var scope = Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<JaimesDbContext>();
            await context.Database.EnsureCreatedAsync();
            await SeedTestDataAsync(context);
        }
    }

    protected virtual async Task SeedTestDataAsync(JaimesDbContext context)
    {
        // Add default test data
        context.Rulesets.Add(new Ruleset { Id = "test-ruleset", Name = "Test Ruleset" });
        context.Players.Add(new Player { Id = "test-player", RulesetId = "test-ruleset" });
        context.Scenarios.Add(new Scenario { Id = "test-scenario", RulesetId = "test-ruleset" });
        await context.SaveChangesAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
