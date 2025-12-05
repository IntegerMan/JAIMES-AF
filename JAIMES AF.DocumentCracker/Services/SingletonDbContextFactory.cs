using MattEland.Jaimes.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.DocumentCracker.Services;

/// <summary>
/// Factory that wraps a singleton DbContext for use with services that require IDbContextFactory.
/// This is used by the console app which uses a singleton DbContext.
/// </summary>
public sealed class SingletonDbContextFactory : IDbContextFactory<JaimesDbContext>
{
    private readonly JaimesDbContext _dbContext;

    public SingletonDbContextFactory(JaimesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public JaimesDbContext CreateDbContext()
    {
        return _dbContext;
    }

    public async Task<JaimesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return _dbContext;
    }
}
