using Microsoft.Agents.AI;
using MudBlazor.Services;
using Microsoft.Agents.AI.AGUI;
using Microsoft.Extensions.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Register a named HttpClient that targets the Aspire API resource name 'apiservice'.
// Service discovery handlers added by AddServiceDefaults will resolve this logical host to the real address at runtime.
// Configure longer timeout for AI chat requests which can take significant time
TimeSpan httpClientTimeout = TimeSpan.FromMinutes(5); // 5 minutes for AI chat requests
if (builder.Configuration["ApiService:TimeoutMinutes"] != null
    && int.TryParse(builder.Configuration["ApiService:TimeoutMinutes"], out int timeoutMinutes))
    httpClientTimeout = TimeSpan.FromMinutes(timeoutMinutes);

builder.Services.AddHttpClient("Api",
        client =>
        {
            // Allow configuring an override in configuration if needed; otherwise use the Aspire project reference name
            client.BaseAddress = new Uri(builder.Configuration["ApiService:BaseAddress"] ?? "http://apiservice/");
            // Set longer timeout for AI chat requests which can take significant time to process
            client.Timeout = httpClientTimeout;
        })
// Ensure service discovery is added on IHttpClientBuilder, then add resilience pipeline
    .AddServiceDiscovery()
    .AddStandardResilienceHandler(options =>
    {
        // Use the same retry logic as the default configuration
        Extensions.ConfigureResilienceHandlerExcludingPost(options);

        // Override attempt and total timeouts so the resilience pipeline allows long-running requests
        TimeSpan attemptTimeout = httpClientTimeout;
        options.AttemptTimeout.Timeout = attemptTimeout;
        options.TotalRequestTimeout.Timeout = attemptTimeout.Add(TimeSpan.FromMinutes(1));

        // Match circuit breaker sampling duration requirements (must be at least double attempt timeout)
        options.CircuitBreaker.SamplingDuration = attemptTimeout + attemptTimeout;
    });

// Make the named client the default HttpClient that's injected with `@inject HttpClient Http`
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

// Configure AG UI integration
builder.Services.AddScoped<AGUIChatClient>(sp =>
{
    HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AGUI");
    string serverUrl = (builder.Configuration["ApiService:BaseAddress"] ?? "http://apiservice/") + "agui";
    AGUIChatClient chatClient = new(httpClient, serverUrl);
    return chatClient;
});
builder.Services.AddScoped<AIAgent>(sp =>
{
    AGUIChatClient chatClient = sp.GetRequiredService<AGUIChatClient>();
    return chatClient.CreateAIAgent(
        name: "agui-client",
        description: "AG-UI Client Agent");
});
builder.Services.AddScoped<AgentThread>(sp =>
{
    AIAgent agent = sp.GetRequiredService<AIAgent>();
    return agent.GetNewThread();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR timeouts for InteractiveServer components
// This allows longer-running operations without timing out
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    // Increase the timeout for server-side operations
    // Default is often 10 seconds, increase to match our HTTP client timeout
    options.ClientTimeoutInterval = httpClientTimeout.Add(TimeSpan.FromMinutes(1));
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddMudServices();

builder.Services.AddOutputCache();

// Configure Kestrel request timeout to allow longer operations
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Increase the request timeout to match our HTTP client timeout
    // This prevents Kestrel from timing out long-running requests
    serverOptions.Limits.RequestHeadersTimeout = httpClientTimeout.Add(TimeSpan.FromMinutes(1));
    serverOptions.Limits.KeepAliveTimeout = httpClientTimeout.Add(TimeSpan.FromMinutes(2));
});

// Configure ASP.NET Core request timeout middleware
// Note: RequestTimeout middleware is optional - SignalR and Kestrel timeouts are more critical
builder.Services.AddRequestTimeouts();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
