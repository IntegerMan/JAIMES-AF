    // Use polling instead of inotify to avoid watcher limits
    Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

    IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
    
    IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis-embeddings", "redis/redis-stack:latest")
        .WithHttpEndpoint(8001, 8001, name: "redisinsight")
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🔬 RedisInsight")
        .WithEndpoint(6379, 6379, name: "redis")
        .WithBindMount(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspire", "jaimes-redis-data"), "/data")
        .WithIconName("BookDatabase");

    IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("apiservice")
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
        .WaitFor(redis)
        .WithEnvironment("VectorDb__ConnectionString", "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=10000")
        // Configure debug-level logging for user namespaces and Agent Framework
        // Note: Dots in namespace names must be replaced with double underscores in environment variables
        .WithEnvironment("Logging__LogLevel__MattEland__Jaimes", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Agents__AI", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Extensions__AI", "Debug");

    builder.AddProject<Projects.JAIMES_AF_Web>("webfrontend")
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
        .WaitFor(apiService)
        // Configure debug-level logging for user namespaces and Agent Framework
        // Note: Dots in namespace names must be replaced with double underscores in environment variables
        .WithEnvironment("Logging__LogLevel__MattEland__Jaimes", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Agents__AI", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Extensions__AI", "Debug");

    IResourceBuilder<ProjectResource> indexer = builder.AddProject<Projects.JAIMES_AF_Indexer>("indexer")
        .WithIconName("DocumentSearch", IconVariant.Regular)
        .WaitFor(redis)
        .WithExplicitStart()
        // Connect to Redis - using localhost since Redis container is accessible on localhost when managed by Aspire
        .WithEnvironment("Indexer__VectorDbConnectionString", "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=10000")
        // Configure debug-level logging for user namespaces
        .WithEnvironment("Logging__LogLevel__MattEland__Jaimes", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Extensions__AI", "Debug");

    var app = builder.Build();

    app.Run();
