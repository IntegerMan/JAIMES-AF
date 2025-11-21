try
{
    Console.WriteLine("Starting Aspire AppHost...");
    
    // Use polling instead of inotify to avoid watcher limits
    Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1", EnvironmentVariableTarget.Process);
    
    // Aspire will auto-detect the container runtime (Docker or Podman)
    // Since Podman machine is already running, Aspire will use Podman automatically
    Console.WriteLine("Using container runtime (Docker or Podman will be auto-detected by Aspire)");

    IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
    
    Console.WriteLine("Adding Redis Stack with persistence...");
    IResourceBuilder<ContainerResource> redis = builder.AddContainer("redis", "redis/redis-stack:latest")
        .WithHttpEndpoint(8001, 8001, name: "redisinsight")
        .WithEndpoint(6379, 6379, name: "redis")
        .WithBindMount(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aspire", "jaimes-redis-data"), "/data")
        .WithIconName("Redis", IconVariant.Regular);

    Console.WriteLine("Adding API service...");
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

    Console.WriteLine("Adding Web frontend...");
    builder.AddProject<Projects.JAIMES_AF_Web>("webfrontend")
        .WithIconName("AppGeneric", IconVariant.Filled)
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health")
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/health",
            DisplayText = "👨‍⚕️ Health"
        })
        .WithUrlForEndpoint("http", static url => url.DisplayText = "🏠 Home")
        //.WithUrlForEndpoint("https", static url => url.DisplayText = "🔑 Home (HTTPS)")
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
        /*
        .WithUrlForEndpoint("http", static _ => new()
        {
            Url = "/rulesets",
            DisplayText = "📋 Rulesets"
        })
        */
        .WithReference(apiService)
        .WaitFor(apiService)
        // Configure debug-level logging for user namespaces and Agent Framework
        // Note: Dots in namespace names must be replaced with double underscores in environment variables
        .WithEnvironment("Logging__LogLevel__MattEland__Jaimes", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Agents__AI", "Debug")
        .WithEnvironment("Logging__LogLevel__Microsoft__Extensions__AI", "Debug");

    Console.WriteLine("Building distributed application...");
    var app = builder.Build();
    
    Console.WriteLine("Starting application...");
    Console.WriteLine("The Aspire dashboard should be available shortly. Check the console output for the dashboard URL.");
    
    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("==========================================");
    Console.Error.WriteLine("ERROR: Failed to start Aspire AppHost");
    Console.Error.WriteLine("==========================================");
    Console.Error.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.Error.WriteLine($"Message: {ex.Message}");
    Console.Error.WriteLine($"Stack Trace:");
    Console.Error.WriteLine(ex.StackTrace);
    
    if (ex.InnerException != null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Inner Exception:");
        Console.Error.WriteLine($"  Type: {ex.InnerException.GetType().Name}");
        Console.Error.WriteLine($"  Message: {ex.InnerException.Message}");
        Console.Error.WriteLine($"  Stack Trace: {ex.InnerException.StackTrace}");
    }
    
    Console.Error.WriteLine("==========================================");
    
    // Re-throw so the debugger can catch it
    throw;
}
