using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Indexer.Services;

public class ChangeTracker : IChangeTracker
{
    private readonly ILogger<ChangeTracker> _logger;
    private readonly string _trackingFilePath;
    private readonly ConcurrentDictionary<string, DocumentState> _cache = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public ChangeTracker(ILogger<ChangeTracker> logger, string trackingFilePath)
    {
        _logger = logger;
        _trackingFilePath = trackingFilePath;
        _ = Task.Run(async () => await LoadTrackingDataAsync(CancellationToken.None));
    }

    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToBase64String(hashBytes);
    }

    public async Task<DocumentState?> GetDocumentStateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(filePath, out DocumentState? state))
        {
            return state;
        }

        await LoadTrackingDataAsync(cancellationToken);
        _cache.TryGetValue(filePath, out state);
        return state;
    }

    public async Task SaveDocumentStateAsync(DocumentState state, CancellationToken cancellationToken = default)
    {
        _cache[state.FilePath] = state;
        await SaveTrackingDataAsync(cancellationToken);
    }

    private async Task LoadTrackingDataAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_trackingFilePath))
        {
            _logger.LogDebug("Tracking file does not exist, starting with empty state: {TrackingFilePath}", _trackingFilePath);
            return;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            string json = await File.ReadAllTextAsync(_trackingFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            Dictionary<string, DocumentState>? states = JsonSerializer.Deserialize<Dictionary<string, DocumentState>>(json);
            if (states != null)
            {
                foreach ((string key, DocumentState value) in states)
                {
                    _cache[key] = value;
                }
                _logger.LogInformation("Loaded {Count} document states from tracking file", _cache.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tracking data from {TrackingFilePath}", _trackingFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveTrackingDataAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, DocumentState> states = _cache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            string json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_trackingFilePath, json, cancellationToken);
            _logger.LogDebug("Saved {Count} document states to tracking file", states.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tracking data to {TrackingFilePath}", _trackingFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}

