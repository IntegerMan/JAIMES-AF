using FastEndpoints;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.ServiceLayer.Services;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore;

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
        builder.Services.AddSwaggerGen();

        builder.Services.AddFastEndpoints();

        // Add Jaimes repositories and services
        builder.Services.AddJaimesRepositories(builder.Configuration);
        builder.Services.AddJaimesServices();

        WebApplication app = builder.Build();

        // Initialize database unless the configuration explicitly requests skipping initialization (useful for tests)
        bool skipDbInit = app.Configuration.GetValue<bool>("SkipDatabaseInitialization");
        if (!skipDbInit)
        {
            await app.Services.InitializeDatabaseAsync();
        }

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapDefaultEndpoints();
        app.UseFastEndpoints();

        await app.RunAsync();
    }
}
