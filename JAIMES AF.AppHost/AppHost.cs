IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

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

try
{
    builder.Build().Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);

    // "One or more errors occurred. (Failed to configure dashboard resource because ASPNETCORE_URLS environment variable was not set.; Failed to configure dashboard resource because ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL and ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL environment variables are not set. At least one OTLP endpoint must be provided.)"
}
