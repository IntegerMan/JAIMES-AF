using System;
using FastEndpoints;
using FastEndpoints.Swagger;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceDefaults;
using System.Diagnostics;
using MattEland.Jaimes.ApiService.Helpers;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Services;
using MassTransit;
using MongoDB.Driver;

namespace MattEland.Jaimes.ApiService;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add service defaults & Aspire client integrations.
        builder.AddServiceDefaults();

        // Add Seq endpoint only when configuration is available (e.g., running under Aspire)
        string? seqServerUrl = builder.Configuration["Aspire:Resources:seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqServerUrl))
        {
            builder.AddSeqEndpoint("seq");
        }

        // Add MongoDB client integration when connection information is available (Aspire/local config)
        string? mongoConnectionString = builder.Configuration.GetConnectionString("documents")
            ?? builder.Configuration["ConnectionStrings:documents"]
            ?? builder.Configuration["ConnectionStrings__documents"]
            ?? builder.Configuration["Aspire:MongoDB:Driver:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            builder.AddMongoDBClient("documents");
        }
        else
        {
            builder.Services.AddSingleton<IMongoClient>(_ =>
                new MongoClient("mongodb://localhost:27017"));
        }

        string? messagingConnectionString = builder.Configuration.GetConnectionString("messaging")
            ?? builder.Configuration["ConnectionStrings:messaging"]
            ?? builder.Configuration["ConnectionStrings__messaging"];
        bool rabbitMqConfigured = !string.IsNullOrWhiteSpace(messagingConnectionString);

        // Configure MassTransit for publishing messages (RabbitMQ when available, in-memory otherwise)
        builder.Services.AddMassTransit(x =>
        {
            if (rabbitMqConfigured)
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    ConfigureRabbitMq(cfg, messagingConnectionString!);
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
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

    private static void ConfigureRabbitMq(IRabbitMqBusFactoryConfigurator cfg, string connectionString)
    {
        if (cfg == null)
        {
            throw new ArgumentNullException(nameof(cfg));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
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
    }
}