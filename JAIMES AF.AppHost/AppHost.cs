    // Use polling instead of inotify to avoid watcher limits
    Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);

    IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
    
    // NOTE: There is an Aspire integration for Redis, but it doesn't support Redis-Stack. If you customize the image, it still doesn't start Redis-Stack afterwards.
    // It's simpler just to use a known good image with good default behavior.
    IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis-embeddings", "redis/redis-stack:latest")
        .WithIconName("BookDatabase")
        .WithHttpEndpoint(8001, 8001, name: "redisinsight")
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🔬 RedisInsight")
        .WithEndpoint(6379, 6379, name: "redis")
        .WithBindMount(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspire", "jaimes-redis-data"), "/data");

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
        .WithEnvironment("VectorDb__ConnectionString", "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=10000");

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
        .WaitFor(apiService);

    IResourceBuilder<ProjectResource> indexer = builder.AddProject<Projects.JAIMES_AF_Indexer>("indexer")
        .WithIconName("DocumentSearch", IconVariant.Regular)
        .WaitFor(redis)
        .WithExplicitStart()
        // Connect to Redis - using localhost since Redis container is accessible on localhost when managed by Aspire
        .WithEnvironment("Indexer__VectorDbConnectionString", "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=10000");

    var app = builder.Build();

    app.Run();
