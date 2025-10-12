using FastEndpoints;
using MattEland.Jaimes.ServiceDefaults;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddFastEndpoints();

// Add Jaimes repositories and services
builder.Services.AddJaimesRepositories();
builder.Services.AddJaimesServices();

WebApplication app = builder.Build();

// Initialize database
await app.Services.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.UseFastEndpoints();

app.Run();

// Make Program accessible for testing
public partial class Program { }
