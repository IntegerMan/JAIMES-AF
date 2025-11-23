using FastEndpoints;
using FastEndpoints.Swagger;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using System.Diagnostics;
using MattEland.Jaimes.ApiService.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services;
using MassTransit;

namespace MattEland.Jaimes.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add Seq endpoint for advanced log monitoring
        builder.AddSeqEndpoint("seq");

        // Add MongoDB client integration
        builder.AddMongoDBClient("documents");

        // Configure MassTransit for publishing messages
        builder.Services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                // Get RabbitMQ connection string from Aspire
                string? connectionString = builder.Configuration.GetConnectionString("messaging")
                    ?? builder.Configuration["ConnectionStrings:messaging"]
                    ?? builder.Configuration["ConnectionStrings__messaging"];

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "RabbitMQ connection string is not configured. " +
                        "Expected connection string 'messaging' from Aspire.");
                }

                // Parse connection string (format: amqp://username:password@host:port/vhost)
                Uri rabbitUri = new(connectionString);
                string host = rabbitUri.Host;
                ushort port = rabbitUri.Port > 0 ? (ushort)rabbitUri.Port : (ushort)5672;
                string? username = null;
                string? password = null;
                
                if (!string.IsNullOrEmpty(rabbitUri.UserInfo))
                {
                    string[] userInfo = rabbitUri.UserInfo.Split(':');
                    username = userInfo[0];
                    if (userInfo.Length > 1)
                    {
                        password = userInfo[1];
                    }
                }

                cfg.Host(host, port, "/", h =>
                {
                    if (!string.IsNullOrEmpty(username))
                    {
                        h.Username(username);
                    }
                    if (!string.IsNullOrEmpty(password))
                    {
                        h.Password(password);
                    }
                });

                // Configure endpoints (MassTransit will auto-create exchanges/queues as needed)
                cfg.ConfigureEndpoints(context);
            });
        });

        // Add services to the container.
        builder.Services.AddProblemDetails();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        
        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddFastEndpoints().SwaggerDocument();

        // Register a shared ActivitySource instance with the same name used by OpenTelemetry
        builder.Services.AddSingleton(new ActivitySource(builder.Environment.ApplicationName ?? "Jaimes.ApiService"));

        // Configure JaimesChatOptions from configuration and register instance for DI
        JaimesChatOptions chatOptions = builder.Configuration.GetSection("ChatService").Get<JaimesChatOptions>() ?? throw new InvalidOperationException("ChatService configuration is required");
        builder.Services.AddSingleton(chatOptions);

        // Configure VectorDbOptions from configuration and register instance for DI
        VectorDbOptions vectorDbOptions = builder.Configuration.GetSection("VectorDb").Get<VectorDbOptions>() ?? throw new InvalidOperationException("VectorDb configuration is required");
        builder.Services.AddSingleton(vectorDbOptions);

        // Register Kernel Memory (must be registered before services that depend on it)
        builder.Services.AddKernelMemory();

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        // Register DatabaseInitializer for DI
        builder.Services.AddSingleton<DatabaseInitializer>();

        WebApplication app = builder.Build();

        app.ScheduleDatabaseInitialization();

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