using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Web.Components;
using MudBlazor.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Seq endpoint for advanced log monitoring
builder.AddSeqEndpoint("seq");

// Register a named HttpClient that targets the Aspire API resource name 'apiservice'.
// Service discovery handlers added by AddServiceDefaults will resolve this logical host to the real address at runtime.
// Configure longer timeout for AI chat requests which can take significant time
TimeSpan httpClientTimeout = TimeSpan.FromMinutes(5); // 5 minutes for AI chat requests
if (builder.Configuration["ApiService:TimeoutMinutes"] != null 
    && int.TryParse(builder.Configuration["ApiService:TimeoutMinutes"], out int timeoutMinutes))
{
    httpClientTimeout = TimeSpan.FromMinutes(timeoutMinutes);
}

builder.Services.AddHttpClient("Api", client =>
{
    // Allow configuring an override in configuration if needed; otherwise use the Aspire project reference name
    client.BaseAddress = new Uri(builder.Configuration["ApiService:BaseAddress"] ?? "http://apiservice/");
    // Set longer timeout for AI chat requests which can take significant time to process
    client.Timeout = httpClientTimeout;
})
// Ensure service discovery is added on IHttpClientBuilder, then add resilience pipeline
.AddServiceDiscovery()
.AddStandardResilienceHandler(Extensions.ConfigureResilienceHandlerExcludingPost);

// Make the named client the default HttpClient that's injected with `@inject HttpClient Http`
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddOutputCache();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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
