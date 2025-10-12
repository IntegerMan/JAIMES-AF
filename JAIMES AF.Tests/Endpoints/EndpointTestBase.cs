using Microsoft.AspNetCore.Mvc.Testing;
using MattEland.Jaimes.ApiService;

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
                builder.ConfigureServices(services =>
                {
                    // Use in-memory database for testing
                    services.AddJaimesRepositories("DataSource=:memory:;Cache=Shared");
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
