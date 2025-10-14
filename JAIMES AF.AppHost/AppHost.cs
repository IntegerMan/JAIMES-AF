IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.JAIMES_AF_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.JAIMES_AF_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

try {
    builder.Build().Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);

    // "One or more errors occurred. (Failed to configure dashboard resource because ASPNETCORE_URLS environment variable was not set.; Failed to configure dashboard resource because ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL and ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL environment variables are not set. At least one OTLP endpoint must be provided.)"
}
