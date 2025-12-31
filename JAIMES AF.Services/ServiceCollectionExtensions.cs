using MattEland.Jaimes.Agents.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceLayer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJaimesServices(this IServiceCollection services)
    {
        // Use Scrutor for automatic service registration by convention
        services.Scan(scan => scan
            .FromAssemblyOf<GameService>()
            .AddClasses(classes => classes.InNamespaceOf<GameService>())
            .AsSelfWithInterfaces()
            .WithScopedLifetime());
        services.Scan(scan => scan
            .FromAssemblyOf<ChatService>()
            .AddClasses(classes => classes
                .InNamespaceOf<ChatService>()
                .Where(type => type != typeof(MattEland.Jaimes.Agents.Services.GameConversationMemoryProvider)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies([
                typeof(EvaluatorRegistrar).Assembly,
                typeof(Microsoft.Extensions.AI.Evaluation.Quality.RelevanceTruthAndCompletenessEvaluator).Assembly
            ])
            .AddClasses(classes => classes.AssignableTo<Microsoft.Extensions.AI.Evaluation.IEvaluator>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());


        // Configure our AI Agent
        services.AddSingleton<AIAgent>(s =>
        {
            IChatClient chat = s.GetRequiredService<IChatClient>();

            var logs = s.GetRequiredService<ILoggerFactory>();
            ILogger logger = logs.CreateLogger("JaimesChatClient");
            chat = chat.WrapWithInstrumentation(logger);
            // Get system prompt from somewhere else
            // Link up tools
            return chat.CreateJaimesAgent(logger, "JAIMES-AF",
                "You are an AI game master running a role playing game with the player as the sole human player.");
        });

        return services;
    }
}