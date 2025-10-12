using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesServices(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        return services;
    }
}
