using System.Collections.Concurrent;
using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Scoped service that tracks tool calls during an agent run.
/// Thread-safe implementation using ConcurrentBag for concurrent tool invocations.
/// </summary>
public class ToolCallTracker : IToolCallTracker
{
    private readonly ConcurrentBag<ToolCallRecord> _toolCalls = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public Task RecordToolCallAsync(string toolName, object? input, object? output)
    {
        string? inputJson = null;
        string? outputJson = null;

        try
        {
            if (input != null)
            {
                inputJson = JsonSerializer.Serialize(input, JsonOptions);
            }
        }
        catch (Exception)
        {
            // If serialization fails, store a placeholder
            inputJson = "<serialization failed>";
        }

        try
        {
            if (output != null)
            {
                outputJson = JsonSerializer.Serialize(output, JsonOptions);
            }
        }
        catch (Exception)
        {
            // If serialization fails, store a placeholder
            outputJson = "<serialization failed>";
        }

        ToolCallRecord record = new()
        {
            ToolName = toolName,
            InputJson = inputJson,
            OutputJson = outputJson,
            CreatedAt = DateTime.UtcNow
        };

        _toolCalls.Add(record);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ToolCallRecord>> GetToolCallsAsync()
    {
        return Task.FromResult<IReadOnlyList<ToolCallRecord>>(_toolCalls.ToList());
    }

    public Task ClearAsync()
    {
        // ConcurrentBag doesn't have a Clear method, so we need to create a new one
        // However, since this is a scoped service, it will be disposed after the request
        // For now, we'll just return - the service will be recreated for the next request
        return Task.CompletedTask;
    }
}

