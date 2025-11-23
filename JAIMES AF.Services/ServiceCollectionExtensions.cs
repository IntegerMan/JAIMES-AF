using System.Linq;
using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

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

    public static IServiceCollection AddKernelMemory(this IServiceCollection services)
    {
        // Register IKernelMemory as a singleton using MemoryWebClient to connect to the Kernel Memory container
        // The Kernel Memory service is running in a container and exposed via HTTP
        services.AddSingleton<IKernelMemory>(serviceProvider =>
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger("KernelMemory");

            // Get the Kernel Memory service endpoint from configuration
            // This should be set by Aspire when running in the AppHost context
            string? kernelMemoryEndpoint = configuration["KernelMemory__Endpoint"]
                ?? configuration["ConnectionStrings:kernel-memory"]
                ?? configuration["ConnectionStrings__kernel-memory"];

            if (string.IsNullOrWhiteSpace(kernelMemoryEndpoint))
            {
                // Fallback to default localhost endpoint if not configured
                kernelMemoryEndpoint = "http://localhost:9001";
                logger.LogWarning(
                    "Kernel Memory endpoint not found in configuration. Using default: {Endpoint}",
                    kernelMemoryEndpoint);
            }
            else
            {
                logger.LogInformation("Connecting to Kernel Memory service at: {Endpoint}", kernelMemoryEndpoint);
            }

            // Normalize endpoint URL - remove trailing slash to avoid 404 errors
            string normalizedEndpoint = kernelMemoryEndpoint.TrimEnd('/');

            // Create MemoryWebClient to connect to the Kernel Memory container
            // MemoryWebClient implements IKernelMemory and communicates via HTTP
            MemoryWebClient memory = new(normalizedEndpoint);

            logger.LogInformation("✅ MemoryWebClient initialized and connected to Kernel Memory service");

            return memory;
        });

        return services;
    }
}
