using System.Linq;
using MattEland.Jaimes.Agents.Services;
using MattEland.Jaimes.ServiceLayer.Services;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Redis;
using Microsoft.KernelMemory.MemoryStorage;

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

            // Wrap Redis memory database with telemetry before building Kernel Memory
            // We need to create the Redis memory database manually to wrap it
            ILogger<TelemetryRedisMemoryDb> logger = serviceProvider.GetRequiredService<ILogger<TelemetryRedisMemoryDb>>();
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            ILogger kernelMemoryLogger = loggerFactory.CreateLogger("KernelMemory");
            
            // Create the Redis memory database using the builder's internal factory
            // Since RedisMemory constructor is not public, we'll use WithRedisMemoryDb and extract it
            // Note: We need to include text generation in the temp builder for it to build successfully
            IKernelMemoryBuilder tempBuilder = new KernelMemoryBuilder()
                .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
                .WithAzureOpenAITextGeneration(textGenerationConfig)
                .WithRedisMemoryDb(redisConfig);
            
            // Build temporarily to get access to the memory database
            IKernelMemory tempMemory = tempBuilder.Build();
            
            // Extract the memory database using reflection
            // The MemoryDb is stored in the _memoryDbs field of handlers within the Orchestrator's _handlers dictionary
            IMemoryDb? originalMemoryDb = null;
            Type memoryType = tempMemory.GetType();
            
            // Navigate: MemoryServerless -> Orchestrator -> _handlers -> handler -> _memoryDbs -> first IMemoryDb
            System.Reflection.PropertyInfo? orchestratorProperty = memoryType.GetProperty(
                "Orchestrator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            if (orchestratorProperty != null)
            {
                object? orchestrator = orchestratorProperty.GetValue(tempMemory);
                if (orchestrator != null)
                {
                    Type orchestratorType = orchestrator.GetType();
                    System.Reflection.FieldInfo? handlersField = orchestratorType.GetField(
                        "_handlers",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    
                    if (handlersField != null && handlersField.GetValue(orchestrator) is System.Collections.IDictionary handlersDict)
                    {
                        // Find a handler with _memoryDbs field (e.g., SaveRecordsHandler, DeleteDocumentHandler)
                        foreach (System.Collections.DictionaryEntry entry in handlersDict)
                        {
                            object? handler = entry.Value;
                            if (handler != null)
                            {
                                Type handlerType = handler.GetType();
                                System.Reflection.FieldInfo? memoryDbsField = handlerType.GetField(
                                    "_memoryDbs",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                
                                if (memoryDbsField != null)
                                {
                                    object? memoryDbsValue = memoryDbsField.GetValue(handler);
                                    if (memoryDbsValue is System.Collections.IEnumerable memoryDbsList)
                                    {
                                        // Get the first IMemoryDb from the list
                                        foreach (object? item in memoryDbsList)
                                        {
                                            if (item is IMemoryDb db)
                                            {
                                                originalMemoryDb = db;
                                                kernelMemoryLogger.LogInformation(
                                                    "Successfully extracted MemoryDb of type {DbType} from handler {HandlerType}",
                                                    db.GetType().Name,
                                                    handlerType.Name);
                                                break;
                                            }
                                        }
                                        
                                        if (originalMemoryDb != null) break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            IKernelMemory memory;
            if (originalMemoryDb != null)
            {
                // Wrap with telemetry
                IMemoryDb telemetryWrappedMemoryDb = new TelemetryRedisMemoryDb(originalMemoryDb);
                logger.LogInformation(
                    "Wrapping MemoryDb {OriginalType} with TelemetryRedisMemoryDb for OpenTelemetry instrumentation",
                    originalMemoryDb.GetType().Name);
                
                // Rebuild Kernel Memory with the wrapped memory database
                memory = new KernelMemoryBuilder()
                    .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
                    .WithAzureOpenAITextGeneration(textGenerationConfig)
                    .WithCustomMemoryDb(telemetryWrappedMemoryDb)
                    .Build();
                
                // Verify the wrapper is actually being used
                IMemoryDb? verifyDb = ExtractMemoryDbForVerification(memory, loggerFactory);
                if (verifyDb != null)
                {
                    string dbTypeName = verifyDb.GetType().Name;
                    if (dbTypeName == "TelemetryRedisMemoryDb")
                    {
                        logger.LogInformation("✅ TelemetryRedisMemoryDb wrapper is active and will instrument Redis operations");
                    }
                    else
                    {
                        logger.LogWarning(
                            "⚠️ MemoryDb type is {DbType}, expected TelemetryRedisMemoryDb. Telemetry may not be active.",
                            dbTypeName);
                    }
                }
            }
            else
            {
                // Fallback: build normally without telemetry wrapper
                kernelMemoryLogger.LogWarning(
                    "Could not extract memory database from {MemoryType} for telemetry wrapping. Available properties: {Properties}. Using unwrapped instance.",
                    memoryType.Name,
                    string.Join(", ", memoryType.GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Select(p => p.Name)));
                memory = new KernelMemoryBuilder()
                    .WithAzureOpenAITextEmbeddingGeneration(embeddingConfig)
                    .WithAzureOpenAITextGeneration(textGenerationConfig)
                    .WithRedisMemoryDb(redisConfig)
                    .Build();
            }

            return memory;
        });

        return services;
    }

    private static IMemoryDb? ExtractMemoryDbForVerification(IKernelMemory memory, ILoggerFactory loggerFactory)
    {
        Type memoryType = memory.GetType();
        
        // Navigate: MemoryServerless -> Orchestrator -> _handlers -> handler -> _memoryDbs -> first IMemoryDb
        System.Reflection.PropertyInfo? orchestratorProperty = memoryType.GetProperty(
            "Orchestrator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        
        if (orchestratorProperty != null)
        {
            object? orchestrator = orchestratorProperty.GetValue(memory);
            if (orchestrator != null)
            {
                Type orchestratorType = orchestrator.GetType();
                System.Reflection.FieldInfo? handlersField = orchestratorType.GetField(
                    "_handlers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (handlersField != null && handlersField.GetValue(orchestrator) is System.Collections.IDictionary handlersDict)
                {
                    // Find a handler with _memoryDbs field
                    foreach (System.Collections.DictionaryEntry entry in handlersDict)
                    {
                        object? handler = entry.Value;
                        if (handler != null)
                        {
                            Type handlerType = handler.GetType();
                            System.Reflection.FieldInfo? memoryDbsField = handlerType.GetField(
                                "_memoryDbs",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                            
                            if (memoryDbsField != null)
                            {
                                object? memoryDbsValue = memoryDbsField.GetValue(handler);
                                if (memoryDbsValue is System.Collections.IEnumerable memoryDbsList)
                                {
                                    // Get the first IMemoryDb from the list
                                    foreach (object? item in memoryDbsList)
                                    {
                                        if (item is IMemoryDb db)
                                        {
                                            return db;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        return null;
    }
}
