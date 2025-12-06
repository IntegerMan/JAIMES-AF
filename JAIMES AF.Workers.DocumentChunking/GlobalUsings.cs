// Global using directives

global using System.Diagnostics;
global using MattEland.Jaimes.Repositories;
global using MattEland.Jaimes.ServiceDefaults;
global using MattEland.Jaimes.ServiceDefinitions.Configuration;
global using MattEland.Jaimes.ServiceDefinitions.Messages;
global using MattEland.Jaimes.ServiceDefinitions.Services;
global using MattEland.Jaimes.Workers.DocumentChunking.Consumers;
global using MattEland.Jaimes.Workers.Services;
global using Microsoft.Extensions.AI;
global using OllamaSharp;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Resources;
global using OpenTelemetry.Trace;
global using RabbitMQ.Client;
global using SemanticChunkerNET;