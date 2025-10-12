using Microsoft.AspNetCore.Mvc.Testing;

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
                    services.AddJaimesRepositories("test.db", DatabaseProvider.Sqlite);
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
