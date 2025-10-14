using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tests.Endpoints;

public abstract class EndpointTestBase : IAsyncLifetime
{
    protected WebApplicationFactory<Program> Factory = null!;
    protected HttpClient Client = null!;

    public virtual ValueTask InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Inject configuration values that Program will read during startup.
                // Set the app's database provider to InMemory for tests.
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    var inMemorySettings = new Dictionary<string, string?>
                    {
                        // The app reads a top-level "DatabaseProvider" key (not "Jaimes:DatabaseProvider").
                        ["DatabaseProvider"] = "inmemory",
                        // The repository registration expects a DefaultConnection connection string to exist
                        // even if InMemory is used, so provide an empty value to satisfy the call.
                        ["ConnectionStrings:DefaultConnection"] = string.Empty,
                        // Tell the app to skip database initialization at startup; the test will control init.
                        ["SkipDatabaseInitialization"] = "true",
                        // Tell the repository registration to skip registering the application's DB provider
                        // so the test can register its own in-memory provider without causing EF Core conflicts.
                        ["SkipDatabaseRegistration"] = "true"
                    };

                    configBuilder.AddInMemoryCollection(inMemorySettings);
                });

                // As a stronger guarantee, replace any existing JaimesDbContext registration with an InMemory one.
                builder.ConfigureServices(services =>
                {
                    // Remove existing registrations for JaimesDbContext and its options
                    var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<JaimesDbContext>));
                    if (optionsDescriptor != null)
                    {
                        services.Remove(optionsDescriptor);
                    }

                    var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(JaimesDbContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }

                    // Register the test in-memory database
                    services.AddDbContext<JaimesDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("InMemoryTest");
                    });
                });
            });

        Client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
