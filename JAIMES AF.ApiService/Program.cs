using FastEndpoints;
using FastEndpoints.Swagger;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.ServiceLayer;
using MattEland.Jaimes.ServiceDefinitions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddProblemDetails();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        
        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance with the same name used by OpenTelemetry
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName ?? "Jaimes.ApiService"));

        // Configure ChatOptions from configuration and register instance for DI
        ChatOptions chatOptions = builder.Configuration.GetSection("ChatService").Get<ChatOptions>() ?? throw new InvalidOperationException("ChatService configuration is required");
        builder.Services.AddSingleton(chatOptions);

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        // Register DatabaseInitializer for DI
        builder.Services.AddSingleton<DatabaseInitializer>();

        WebApplication app = builder.Build();

        // Schedule database initialization to run after the host has started so OpenTelemetry TracerProviders
        // and any ActivityListeners are active and will capture the activities produced during initialization.
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            // Run in background to avoid blocking the ApplicationStarted callback
            _ = Task.Run(async () =>
            {
                try
                {
                    DatabaseInitializer dbInit = app.Services.GetRequiredService<DatabaseInitializer>();
                    await dbInit.InitializeAsync(app);
                }
                catch (Exception ex)
                {
                    ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Database initialization failed during ApplicationStarted.");

                    throw;
                }
            });
        });

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapDefaultEndpoints();
        app.UseFastEndpoints().UseSwaggerGen();

        await app.RunAsync();
    }
}