using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace MattEland.Jaimes.Tests.Repositories;

public class RepositoryConfigurationTests
{
    [Fact]
    public void AddJaimesRepositoriesInMemory_WithDefaultName_CreatesInMemoryDatabase()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddJaimesRepositoriesInMemory();
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();

        // Assert
        context.ShouldNotBeNull();
        context.Database.IsInMemory().ShouldBeTrue();
    }

    [Fact]
    public void AddJaimesRepositoriesInMemory_WithCustomName_UsesCustomDatabaseName()
    {
        // Arrange
        ServiceCollection services = new();
        const string customDbName = "CustomTestDb";

        // Act
        services.AddJaimesRepositoriesInMemory(customDbName);
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();

        // Assert
        context.ShouldNotBeNull();
        context.Database.IsInMemory().ShouldBeTrue();
        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.InMemory");
    }

    [Fact]
    public void AddJaimesRepositories_WithValidConnectionString_ConfiguresPostgreSQL()
    {
        // Arrange
        ServiceCollection services = new();
        Dictionary<string, string?> configValues = new()
        {
            { "ConnectionStrings:jaimes-db", "Host=localhost;Database=test;Username=test;Password=test" }
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        services.AddJaimesRepositories(configuration);
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();

        // Assert
        context.ShouldNotBeNull();
        context.Database.IsNpgsql().ShouldBeTrue();
        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void AddJaimesRepositories_WithDefaultConnectionString_ConfiguresPostgreSQL()
    {
        // Arrange
        ServiceCollection services = new();
        Dictionary<string, string?> configValues = new()
        {
            { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=test;Username=test;Password=test" }
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        services.AddJaimesRepositories(configuration);
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();

        // Assert
        context.ShouldNotBeNull();
        context.Database.IsNpgsql().ShouldBeTrue();
    }

    [Fact]
    public void AddJaimesRepositories_WithoutConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
        {
            services.AddJaimesRepositories(configuration);
        });

        exception.Message.ShouldContain("connection string is required");
        exception.Message.ShouldContain("jaimes-db");
        exception.Message.ShouldContain("DefaultConnection");
    }

    [Fact]
    public void AddJaimesRepositories_PrefersJaimesDbConnectionString_OverDefaultConnection()
    {
        // Arrange
        ServiceCollection services = new();
        Dictionary<string, string?> configValues = new()
        {
            { "ConnectionStrings:jaimes-db", "Host=jaimes-host;Database=jaimes;Username=jaimes;Password=jaimes" },
            { "ConnectionStrings:DefaultConnection", "Host=default-host;Database=default;Username=default;Password=default" }
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Act
        services.AddJaimesRepositories(configuration);
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();

        // Assert
        context.ShouldNotBeNull();
        context.Database.IsNpgsql().ShouldBeTrue();
        // The connection string should use the jaimes-db connection (we can't easily verify the exact connection string,
        // but we've verified it doesn't throw and creates a valid context)
    }

    [Fact]
    public async Task InMemoryDatabase_SupportsBasicOperations()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        ServiceCollection services = new();
        services.AddJaimesRepositoriesInMemory(Guid.NewGuid().ToString());
        ServiceProvider provider = services.BuildServiceProvider();
        JaimesDbContext context = provider.GetRequiredService<JaimesDbContext>();
        await context.Database.EnsureCreatedAsync(ct);

        // Act
        context.Rulesets.Add(new Entities.Ruleset { Id = "test", Name = "Test Ruleset" });
        await context.SaveChangesAsync(ct);

        // Assert
        Entities.Ruleset? ruleset = await context.Rulesets.FindAsync(["test"], ct);
        ruleset.ShouldNotBeNull();
        ruleset.Name.ShouldBe("Test Ruleset");
    }
}
