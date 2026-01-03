using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MattEland.Jaimes.Domain;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service for discovering and executing tools for testing purposes.
/// </summary>
public class ToolTestService(
    IServiceProvider serviceProvider,
    IGameService gameService,
    ILogger<ToolTestService> logger) : IToolTestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public Task<ToolMetadataListResponse> GetRegisteredToolsAsync(CancellationToken cancellationToken = default)
    {
        List<ToolMetadataResponse> tools = [];

        // Scan JAIMES AF.Tools assembly for tools (same pattern as ToolRegistrar)
        Assembly toolsAssembly = typeof(PlayerInfoTool).Assembly;

        var toolMethods = toolsAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("Tool", StringComparison.Ordinal) && t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null)
                .Select(m => new { Type = t, Method = m }))
            .ToList();

        foreach (var toolInfo in toolMethods)
        {
            DescriptionAttribute? descriptionAttr = toolInfo.Method.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttr == null) continue;

            // Extract tool name from method name (e.g., "SearchRulesAsync" -> "SearchRules")
            string name = toolInfo.Method.Name;
            if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^5];
            }

            // Try to figure out category from class name
            string? category = toolInfo.Type.Name.Replace("Tool", "");

            // Check if tool requires game context by looking at constructor parameters
            bool requiresGameContext = toolInfo.Type.GetConstructors()
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(GameDto)));

            // Get parameter info
            List<ToolParameterInfo> parameters = [];
            foreach (ParameterInfo param in toolInfo.Method.GetParameters())
            {
                // Skip CancellationToken parameters
                if (param.ParameterType == typeof(CancellationToken)) continue;

                DescriptionAttribute? paramDesc = param.GetCustomAttribute<DescriptionAttribute>();

                parameters.Add(new ToolParameterInfo
                {
                    Name = param.Name ?? "unknown",
                    TypeName = GetFriendlyTypeName(param.ParameterType),
                    IsRequired = !param.IsOptional && !IsNullable(param.ParameterType),
                    Description = paramDesc?.Description,
                    DefaultValue = param.HasDefaultValue ? param.DefaultValue?.ToString() : null
                });
            }

            tools.Add(new ToolMetadataResponse
            {
                Name = name,
                Description = descriptionAttr.Description,
                Category = category,
                ClassName = toolInfo.Type.Name,
                MethodName = toolInfo.Method.Name,
                Parameters = parameters.ToArray(),
                RequiresGameContext = requiresGameContext
            });
        }

        return Task.FromResult(new ToolMetadataListResponse
        {
            Tools = tools.OrderBy(t => t.Category).ThenBy(t => t.Name).ToArray()
        });
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResponse> ExecuteToolAsync(ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Find the tool method
            Assembly toolsAssembly = typeof(PlayerInfoTool).Assembly;

            var toolMethod = toolsAssembly.GetTypes()
                .Where(t => t.Name.EndsWith("Tool", StringComparison.Ordinal) && t.IsClass && !t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<DescriptionAttribute>() != null)
                    .Select(m => new { Type = t, Method = m }))
                .FirstOrDefault(x =>
                {
                    string methodName = x.Method.Name;
                    if (methodName.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                    {
                        methodName = methodName[..^5];
                    }
                    return methodName.Equals(request.ToolName, StringComparison.OrdinalIgnoreCase);
                });

            if (toolMethod == null)
            {
                stopwatch.Stop();
                return new ToolExecutionResponse
                {
                    Success = false,
                    ErrorMessage = $"Tool '{request.ToolName}' not found.",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ToolName = request.ToolName
                };
            }

            // Check if tool requires game context
            bool requiresGameContext = toolMethod.Type.GetConstructors()
                .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(GameDto)));

            GameDto? game = null;
            if (requiresGameContext)
            {
                if (!request.GameId.HasValue)
                {
                    stopwatch.Stop();
                    return new ToolExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = "This tool requires a game context. Please select a game.",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        ToolName = request.ToolName
                    };
                }

                game = await gameService.GetGameAsync(request.GameId.Value, cancellationToken);
                if (game == null)
                {
                    stopwatch.Stop();
                    return new ToolExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Game with ID '{request.GameId.Value}' not found.",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        ToolName = request.ToolName
                    };
                }
            }

            // Create tool instance
            object? toolInstance = CreateToolInstance(toolMethod.Type, game);
            if (toolInstance == null)
            {
                stopwatch.Stop();
                return new ToolExecutionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create instance of tool '{toolMethod.Type.Name}'.",
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    ToolName = request.ToolName
                };
            }

            // Build method parameters
            ParameterInfo[] methodParams = toolMethod.Method.GetParameters();
            object?[] parameterValues = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                ParameterInfo param = methodParams[i];

                // Handle CancellationToken
                if (param.ParameterType == typeof(CancellationToken))
                {
                    parameterValues[i] = cancellationToken;
                    continue;
                }

                // Get value from request
                if (request.Parameters.TryGetValue(param.Name ?? "", out string? stringValue) && stringValue != null)
                {
                    parameterValues[i] = ConvertParameter(stringValue, param.ParameterType);
                }
                else if (param.HasDefaultValue)
                {
                    parameterValues[i] = param.DefaultValue;
                }
                else if (IsNullable(param.ParameterType))
                {
                    parameterValues[i] = null;
                }
                else
                {
                    stopwatch.Stop();
                    return new ToolExecutionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Required parameter '{param.Name}' was not provided.",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        ToolName = request.ToolName
                    };
                }
            }

            // Invoke the method
            object? result = toolMethod.Method.Invoke(toolInstance, parameterValues);

            // Handle async methods
            if (result is Task task)
            {
                await task;

                // Get result from Task<T>
                Type taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    PropertyInfo? resultProperty = taskType.GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else
                {
                    result = null;
                }
            }

            stopwatch.Stop();

            // Format result
            string? resultString = result switch
            {
                null => null,
                string s => s,
                _ => JsonSerializer.Serialize(result, JsonOptions)
            };

            return new ToolExecutionResponse
            {
                Success = true,
                Result = resultString,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ToolName = request.ToolName
            };
        }
        catch (TargetInvocationException ex)
        {
            stopwatch.Stop();
            logger.LogError(ex.InnerException ?? ex, "Error executing tool {ToolName}", request.ToolName);
            return new ToolExecutionResponse
            {
                Success = false,
                ErrorMessage = ex.InnerException?.Message ?? ex.Message,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ToolName = request.ToolName
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error executing tool {ToolName}", request.ToolName);
            return new ToolExecutionResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ToolName = request.ToolName
            };
        }
    }

    private object? CreateToolInstance(Type toolType, GameDto? game)
    {
        ConstructorInfo[] constructors = toolType.GetConstructors();
        if (constructors.Length == 0) return null;

        // Find the best constructor (prefer one with GameDto if we have a game)
        ConstructorInfo? constructor = constructors
            .OrderByDescending(c => game != null && c.GetParameters().Any(p => p.ParameterType == typeof(GameDto)))
            .ThenByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (constructor == null) return null;

        ParameterInfo[] ctorParams = constructor.GetParameters();
        object?[] ctorArgs = new object?[ctorParams.Length];

        for (int i = 0; i < ctorParams.Length; i++)
        {
            ParameterInfo param = ctorParams[i];

            if (param.ParameterType == typeof(GameDto))
            {
                ctorArgs[i] = game;
            }
            else if (param.ParameterType == typeof(IServiceProvider))
            {
                ctorArgs[i] = serviceProvider;
            }
            else
            {
                // Try to resolve from DI
                ctorArgs[i] = serviceProvider.GetService(param.ParameterType);
            }
        }

        return constructor.Invoke(ctorArgs);
    }

    private static object? ConvertParameter(string value, Type targetType)
    {
        // Handle nullable types
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (string.IsNullOrEmpty(value) && IsNullable(targetType))
        {
            return null;
        }

        if (underlyingType == typeof(string))
        {
            return value;
        }

        if (underlyingType == typeof(int))
        {
            return int.Parse(value);
        }

        if (underlyingType == typeof(long))
        {
            return long.Parse(value);
        }

        if (underlyingType == typeof(double))
        {
            return double.Parse(value);
        }

        if (underlyingType == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (underlyingType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (underlyingType == typeof(DateTime))
        {
            return DateTime.Parse(value);
        }

        // Try JSON deserialization for complex types
        return JsonSerializer.Deserialize(value, targetType);
    }

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        Type? underlyingType = Nullable.GetUnderlyingType(type);
        bool isNullable = underlyingType != null;
        Type actualType = underlyingType ?? type;

        string name = actualType.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Int64" => "long",
            "Double" => "double",
            "Single" => "float",
            "Boolean" => "bool",
            "Guid" => "guid",
            "DateTime" => "datetime",
            _ => actualType.Name
        };

        return isNullable ? $"{name}?" : name;
    }
}
