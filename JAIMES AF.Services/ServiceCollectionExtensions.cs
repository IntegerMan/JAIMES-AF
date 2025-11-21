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
            
            // Extract the memory database using reflection - try multiple property names
            IMemoryDb? originalMemoryDb = null;
            Type memoryType = tempMemory.GetType();
            
            // First, try to get MemoryDb directly from the memory instance
            string[] propertyNames = ["MemoryDb", "_memoryDb", "memoryDb", "Memory", "_memory"];
            foreach (string propertyName in propertyNames)
            {
                System.Reflection.PropertyInfo? property = memoryType.GetProperty(
                    propertyName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (property != null)
                {
                    object? value = property.GetValue(tempMemory);
                    if (value is IMemoryDb db)
                    {
                        originalMemoryDb = db;
                        kernelMemoryLogger.LogInformation(
                            "Successfully extracted MemoryDb of type {DbType} using property {PropertyName}",
                            db.GetType().Name,
                            propertyName);
                        break;
                    }
                }
            }
            
            // If direct access failed, try to navigate through Orchestrator
            if (originalMemoryDb == null)
            {
                System.Reflection.PropertyInfo? orchestratorProperty = memoryType.GetProperty(
                    "Orchestrator",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (orchestratorProperty != null)
                {
                    object? orchestrator = orchestratorProperty.GetValue(tempMemory);
                    if (orchestrator != null)
                    {
                        Type orchestratorType = orchestrator.GetType();
                        kernelMemoryLogger.LogInformation(
                            "Found Orchestrator property of type {OrchestratorType}, searching for MemoryDb within it",
                            orchestratorType.Name);
                        
                        // Log all available properties and fields on Orchestrator for debugging
                        System.Reflection.PropertyInfo[] orchestratorProperties = orchestratorType.GetProperties(
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        System.Reflection.FieldInfo[] orchestratorFields = orchestratorType.GetFields(
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        
                        kernelMemoryLogger.LogInformation(
                            "Orchestrator properties: {Properties}, fields: {Fields}",
                            string.Join(", ", orchestratorProperties.Select(p => $"{p.Name}({p.PropertyType.Name})")),
                            string.Join(", ", orchestratorFields.Select(f => $"{f.Name}({f.FieldType.Name})")));
                        
                        // Try to get MemoryDb from Orchestrator - try ALL properties first
                        foreach (System.Reflection.PropertyInfo property in orchestratorProperties)
                        {
                            try
                            {
                                object? value = property.GetValue(orchestrator);
                                if (value is IMemoryDb db)
                                {
                                    originalMemoryDb = db;
                                    kernelMemoryLogger.LogInformation(
                                        "Successfully extracted MemoryDb of type {DbType} from Orchestrator using property {PropertyName}",
                                        db.GetType().Name,
                                        property.Name);
                                    break;
                                }
                                // Also check if the property type implements IMemoryDb (for nested cases)
                                else if (value != null && typeof(IMemoryDb).IsAssignableFrom(property.PropertyType))
                                {
                                    kernelMemoryLogger.LogDebug(
                                        "Found property {PropertyName} of type {PropertyType} that implements IMemoryDb, but value is null or wrong type",
                                        property.Name,
                                        property.PropertyType.Name);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Property might not be accessible, skip it
                                kernelMemoryLogger.LogDebug(
                                    "Could not access property {PropertyName} on Orchestrator: {Error}",
                                    property.Name,
                                    ex.Message);
                            }
                        }
                        
                        // Try ALL fields on Orchestrator if properties didn't work
                        if (originalMemoryDb == null)
                        {
                            foreach (System.Reflection.FieldInfo field in orchestratorFields)
                            {
                                try
                                {
                                    object? value = field.GetValue(orchestrator);
                                    if (value is IMemoryDb db)
                                    {
                                        originalMemoryDb = db;
                                        kernelMemoryLogger.LogInformation(
                                            "Successfully extracted MemoryDb of type {DbType} from Orchestrator using field {FieldName}",
                                            db.GetType().Name,
                                            field.Name);
                                        break;
                                    }
                                    // Check if this is the _handlers dictionary - MemoryDb might be in one of the handlers
                                    else if (field.Name == "_handlers" && value is System.Collections.IDictionary handlersDict)
                                    {
                                        kernelMemoryLogger.LogInformation(
                                            "Found _handlers dictionary with {Count} handlers, searching for MemoryDb within handlers",
                                            handlersDict.Count);
                                        
                                        foreach (System.Collections.DictionaryEntry entry in handlersDict)
                                        {
                                            object? handler = entry.Value;
                                            if (handler != null)
                                            {
                                                Type handlerType = handler.GetType();
                                                kernelMemoryLogger.LogInformation(
                                                    "Checking handler {HandlerType} (key: {Key})",
                                                    handlerType.Name,
                                                    entry.Key);
                                                
                                                // Try to get MemoryDb from the handler
                                                System.Reflection.PropertyInfo[] handlerProperties = handlerType.GetProperties(
                                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                                System.Reflection.FieldInfo[] handlerFields = handlerType.GetFields(
                                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                                                
                                                kernelMemoryLogger.LogInformation(
                                                    "Handler {HandlerType} has properties: {Properties}, fields: {Fields}",
                                                    handlerType.Name,
                                                    string.Join(", ", handlerProperties.Select(p => $"{p.Name}({p.PropertyType.Name})")),
                                                    string.Join(", ", handlerFields.Select(f => $"{f.Name}({f.FieldType.Name})")));
                                                
                                                foreach (System.Reflection.PropertyInfo handlerProperty in handlerProperties)
                                                {
                                                    try
                                                    {
                                                        object? handlerValue = handlerProperty.GetValue(handler);
                                                        if (handlerValue is IMemoryDb handlerDb)
                                                        {
                                                            originalMemoryDb = handlerDb;
                                                            kernelMemoryLogger.LogInformation(
                                                                "Successfully extracted MemoryDb of type {DbType} from handler {HandlerType} using property {PropertyName}",
                                                                handlerDb.GetType().Name,
                                                                handlerType.Name,
                                                                handlerProperty.Name);
                                                            break;
                                                        }
                                                        // Check if it's a list of IMemoryDb
                                                        else if (handlerValue is System.Collections.IEnumerable enumerable)
                                                        {
                                                            foreach (object? item in enumerable)
                                                            {
                                                                if (item is IMemoryDb listDb)
                                                                {
                                                                    originalMemoryDb = listDb;
                                                                    kernelMemoryLogger.LogInformation(
                                                                        "Successfully extracted MemoryDb of type {DbType} from handler {HandlerType} using property {PropertyName} (from list)",
                                                                        listDb.GetType().Name,
                                                                        handlerType.Name,
                                                                        handlerProperty.Name);
                                                                    break;
                                                                }
                                                            }
                                                            if (originalMemoryDb != null) break;
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        kernelMemoryLogger.LogDebug(
                                                            "Could not access property {PropertyName} on handler {HandlerType}: {Error}",
                                                            handlerProperty.Name,
                                                            handlerType.Name,
                                                            ex.Message);
                                                    }
                                                }
                                                
                                                if (originalMemoryDb != null) break;
                                                
                                                // Also try fields on the handler
                                                foreach (System.Reflection.FieldInfo handlerField in handlerFields)
                                                {
                                                    try
                                                    {
                                                        object? handlerValue = handlerField.GetValue(handler);
                                                        if (handlerValue is IMemoryDb handlerFieldDb)
                                                        {
                                                            originalMemoryDb = handlerFieldDb;
                                                            kernelMemoryLogger.LogInformation(
                                                                "Successfully extracted MemoryDb of type {DbType} from handler {HandlerType} using field {FieldName}",
                                                                handlerFieldDb.GetType().Name,
                                                                handlerType.Name,
                                                                handlerField.Name);
                                                            break;
                                                        }
                                                        // Check if it's a list of IMemoryDb
                                                        else if (handlerValue is System.Collections.IEnumerable enumerable)
                                                        {
                                                            foreach (object? item in enumerable)
                                                            {
                                                                if (item is IMemoryDb listDb)
                                                                {
                                                                    originalMemoryDb = listDb;
                                                                    kernelMemoryLogger.LogInformation(
                                                                        "Successfully extracted MemoryDb of type {DbType} from handler {HandlerType} using field {FieldName} (from list)",
                                                                        listDb.GetType().Name,
                                                                        handlerType.Name,
                                                                        handlerField.Name);
                                                                    break;
                                                                }
                                                            }
                                                            if (originalMemoryDb != null) break;
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        kernelMemoryLogger.LogDebug(
                                                            "Could not access field {FieldName} on handler {HandlerType}: {Error}",
                                                            handlerField.Name,
                                                            handlerType.Name,
                                                            ex.Message);
                                                    }
                                                }
                                                
                                                if (originalMemoryDb != null) break;
                                            }
                                        }
                                        
                                        if (originalMemoryDb != null) break;
                                    }
                                    // Also check if the field type implements IMemoryDb
                                    else if (value != null && typeof(IMemoryDb).IsAssignableFrom(field.FieldType))
                                    {
                                        kernelMemoryLogger.LogDebug(
                                            "Found field {FieldName} of type {FieldType} that implements IMemoryDb, but value is null or wrong type",
                                            field.Name,
                                            field.FieldType.Name);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Field might not be accessible, skip it
                                    kernelMemoryLogger.LogDebug(
                                        "Could not access field {FieldName} on Orchestrator: {Error}",
                                        field.Name,
                                        ex.Message);
                                }
                            }
                        }
                    }
                }
            }
            
            // If property access failed, try field access on the memory instance itself
            if (originalMemoryDb == null)
            {
                foreach (string fieldName in propertyNames)
                {
                    System.Reflection.FieldInfo? field = memoryType.GetField(
                        fieldName,
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    
                    if (field != null)
                    {
                        object? value = field.GetValue(tempMemory);
                        if (value is IMemoryDb db)
                        {
                            originalMemoryDb = db;
                            kernelMemoryLogger.LogInformation(
                                "Successfully extracted MemoryDb of type {DbType} using field {FieldName}",
                                db.GetType().Name,
                                fieldName);
                            break;
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
        
        // Try to extract MemoryDb for verification - first try direct access
        string[] propertyNames = ["MemoryDb", "_memoryDb", "memoryDb", "Memory", "_memory"];
        foreach (string propertyName in propertyNames)
        {
            System.Reflection.PropertyInfo? property = memoryType.GetProperty(
                propertyName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            if (property != null)
            {
                object? value = property.GetValue(memory);
                if (value is IMemoryDb db)
                {
                    return db;
                }
            }
        }
        
        // Try navigating through Orchestrator
        System.Reflection.PropertyInfo? orchestratorProperty = memoryType.GetProperty(
            "Orchestrator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        
        if (orchestratorProperty != null)
        {
            object? orchestrator = orchestratorProperty.GetValue(memory);
            if (orchestrator != null)
            {
                Type orchestratorType = orchestrator.GetType();
                
                // Try properties on Orchestrator
                foreach (string propertyName in propertyNames)
                {
                    System.Reflection.PropertyInfo? property = orchestratorType.GetProperty(
                        propertyName,
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    
                    if (property != null)
                    {
                        object? value = property.GetValue(orchestrator);
                        if (value is IMemoryDb db)
                        {
                            return db;
                        }
                    }
                }
                
                // Try fields on Orchestrator
                foreach (string fieldName in propertyNames)
                {
                    System.Reflection.FieldInfo? field = orchestratorType.GetField(
                        fieldName,
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    
                    if (field != null)
                    {
                        object? value = field.GetValue(orchestrator);
                        if (value is IMemoryDb db)
                        {
                            return db;
                        }
                    }
                }
            }
        }
        
        // Try fields on memory instance itself
        foreach (string fieldName in propertyNames)
        {
            System.Reflection.FieldInfo? field = memoryType.GetField(
                fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            if (field != null)
            {
                object? value = field.GetValue(memory);
                if (value is IMemoryDb db)
                {
                    return db;
                }
            }
        }
        
        return null;
    }
}
