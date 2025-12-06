namespace MattEland.Jaimes.DocumentCracker.Services;

/// <summary>
/// Factory that creates new DbContext instances for use with services that require IDbContextFactory.
/// Each call returns a new instance that can be safely disposed, while all instances share the same connection configuration.
/// </summary>
public sealed class SingletonDbContextFactory(DbContextOptions<JaimesDbContext> options)
    : IDbContextFactory<JaimesDbContext>
{
    public JaimesDbContext CreateDbContext()
    {
        return new JaimesDbContext(options);
    }

    public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return new JaimesDbContext(options);
    }
}