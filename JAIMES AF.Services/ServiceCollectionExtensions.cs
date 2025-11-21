using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Redis;

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
        // Register IKernelMemory as a singleton so it can be shared across services
        // Kernel Memory is thread-safe and designed to be used as a singleton
        services.AddSingleton<IKernelMemory>(serviceProvider =>
        {
            JaimesChatOptions chatOptions = serviceProvider.GetRequiredService<JaimesChatOptions>();
            VectorDbOptions vectorDbOptions = serviceProvider.GetRequiredService<VectorDbOptions>();

            // Configure Kernel Memory with Azure OpenAI
            // Reference: https://blog.leadingedje.com/post/ai/documents/kernelmemory.html
            // Normalize endpoint URL - remove trailing slash to avoid 404 errors
            string normalizedEndpoint = chatOptions.Endpoint.TrimEnd('/');

            // Create separate configs for embedding and text generation since they use different deployments
            // Use centralized helper to ensure embedding dimensions match between Indexer and Services
            AzureOpenAIConfig embeddingConfig = EmbeddingConfigHelper.CreateEmbeddingConfig(
                apiKey: chatOptions.ApiKey,
                endpoint: normalizedEndpoint,
                deployment: chatOptions.EmbeddingDeployment);

            AzureOpenAIConfig textGenerationConfig = new()
            {
                APIKey = chatOptions.ApiKey,
                Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                Endpoint = normalizedEndpoint,
                Deployment = chatOptions.TextGenerationDeployment,
            };

            // Use Redis as the vector store for Kernel Memory
            // Redis provides better performance and document listing capabilities than SimpleVectorDb
            // Connection string format: "localhost:6379" or "localhost:6379,password=xxx" or full connection string
            string redisConnectionString = vectorDbOptions.ConnectionString;
            
            // If connection string is in old format (Data Source=...), extract just the path
            // Otherwise use as-is for Redis connection string
            if (redisConnectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy format - convert to Redis default
                redisConnectionString = "localhost:6379";
            }

            // Use centralized RedisConfig creation to ensure consistency
            RedisConfig redisConfig = RedisConfigHelper.CreateRedisConfig(redisConnectionString);

            // Use Redis extension method from the Redis package
            IKernelMemory memory = new KernelMemoryBuilder()
                .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
                .WithAzureOpenAITextGeneration(textGenerationConfig)
                .WithRedisMemoryDb(redisConfig)
                .Build();

            return memory;
        });

        return services;
    }
}
