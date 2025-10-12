using Microsoft.EntityFrameworkCore;
using MattEland.Jaimes.Repositories;

namespace MattEland.Jaimes.Tests.Repositories;

public abstract class RepositoryTestBase : IAsyncLifetime
{
    protected JaimesDbContext Context = null!;

    public virtual async ValueTask InitializeAsync()
    {
        // Create an in-memory database for testing
        DbContextOptions<JaimesDbContext> options = new DbContextOptionsBuilder<JaimesDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        Context = new JaimesDbContext(options);
        await Context.Database.OpenConnectionAsync();
        await Context.Database.EnsureCreatedAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Context.Database.CloseConnectionAsync();
        await Context.DisposeAsync();
    }
}
