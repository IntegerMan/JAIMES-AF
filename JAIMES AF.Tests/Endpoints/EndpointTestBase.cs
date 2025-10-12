using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
                        ["Jaimes:DatabaseProvider"] = "WEIRD"
                    };

                    configBuilder.AddInMemoryCollection(inMemorySettings);
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
