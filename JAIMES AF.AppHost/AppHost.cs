try
{
    Console.WriteLine("Starting Aspire AppHost...");
    
    IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
    
    Console.WriteLine("Adding API service...");
    IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("apiservice")
        .WithIconName("DocumentGlobe", IconVariant.Regular)
        .WithExternalHttpEndpoints()
        .WithUrls(u => u.Urls.Clear())
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
        });

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
        .WithUrlForEndpoint("https", static url => url.DisplayText = "🔑 Home (HTTPS)")
        .WithReference(apiService)
        .WaitFor(apiService);

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
