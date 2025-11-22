    // Use polling instead of inotify to avoid watcher limits
    Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

    IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
    
    // Add Ollama with nomic-embed-text model for embeddings
    IResourceBuilder<OllamaResource> ollama = builder.AddOllama("ollama-models")
        //.WithIconName("BrainCircuit")
        // .WithContainerRuntimeArgs("--device", "nvidia.com/gpu=all") // Podman GPU config
        .WithDataVolume()
        .WithOpenWebUI(); // Persist models across container restarts
    
    var embedModel = ollama.AddModel("nomic-embed-text").WithIconName("DocumentTextExtract");
    var chatModel = ollama.AddModel("gemma3").WithIconName("CommentText");
    
    // NOTE: There is an Aspire integration for Redis, but it doesn't support Redis-Stack. If you customize the image, it still doesn't start Redis-Stack afterwards.
    // It's simpler just to use a known good image with good default behavior.
    IResourceBuilder<ContainerResource> redis = builder.AddContainer("embeddings", "redis/redis-stack:latest")
        .WithIconName("BookDatabase")
        .WithHttpEndpoint(8001, 8001, name: "redisinsight")
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🔬 RedisInsight")
        .WithEndpoint(6379, 6379, name: "redis")
        .WithBindMount(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspire", "jaimes-redis-data"), "/data");

    // Helper function to create standardized Redis connection string from endpoint reference
    // Note: ContainerResource doesn't support WithReference directly, so we use WithEnvironment with endpoint references
    static string CreateRedisConnectionString(EndpointReference endpoint) =>
        $"{endpoint.Host}:{endpoint.Port},abortConnect=false,connectRetry=5,connectTimeout=10000";

    IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("jaimes-api")
        .WithIconName("DocumentGlobe", IconVariant.Regular)
        .WithExternalHttpEndpoints()
        //.WithUrls(u => u.Urls.Clear())
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🌳 Root")
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/openapi/v1.json",
            DisplayText = "🌐 OpenAPI"
        })
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/swagger",
            DisplayText = "📃 Swagger"
        })
        .WithHttpHealthCheck("/health")
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/health",
            DisplayText = "👨‍⚕️ Health"
        })
        .WithReference(chatModel)
        .WithReference(embedModel)
        .WaitFor(redis)
        .WaitFor(ollama)
        .WithEnvironment(context =>
        {
            EndpointReference redisEndpoint = redis.GetEndpoint("redis");
            context.EnvironmentVariables["VectorDb__ConnectionString"] = CreateRedisConnectionString(redisEndpoint);
        });

    builder.AddProject<Projects.JAIMES_AF_Web>("jaimes-chat")
        .WithIconName("GameChat")
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health")
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/health",
            DisplayText = "👨‍⚕️ Health"
        })
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🏠 Home")
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/games",
            DisplayText = "🎮 Games"
        })
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/admin",
            DisplayText = "⚙️ Admin"
        })
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/scenarios",
            DisplayText = "📖 Scenarios"
        })
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/players",
            DisplayText = "👤 Players"
        })
        .WithReference(apiService)
        .WaitFor(apiService);

    builder.AddProject<Projects.JAIMES_AF_Indexer>("indexer")
        .WithIconName("DocumentSearch", IconVariant.Regular)
        .WithExplicitStart()
        .WithReference(embedModel)
        .WaitFor(ollama)
        .WithEnvironment(context =>
        {
            EndpointReference redisEndpoint = redis.GetEndpoint("redis");
            context.EnvironmentVariables["Indexer__VectorDbConnectionString"] = CreateRedisConnectionString(redisEndpoint);
        });

    var app = builder.Build();

    app.Run();
