using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceLayer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesServices(this IServiceCollection services)
    {
        // Use Scrutor for automatic service registration by convention
        services.Scan(scan => scan
            .FromAssemblyOf<GameService>()
            .AddClasses(classes => classes.InNamespaceOf<GameService>())
            .AsSelfWithInterfaces()
            .WithScopedLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<ChatService>()
            .AddClasses(classes => classes.InNamespaceOf<ChatService>())
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
