using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.ServiceLayer.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesServices(this IServiceCollection services)
    {
        // Use Scrutor for automatic service registration by convention
        services.Scan(scan => scan
            .FromAssemblyOf<IGameService>()
            .AddClasses(classes => classes.InNamespaceOf<IGameService>())
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        return services;
    }
}
