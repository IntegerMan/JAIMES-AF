using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;

namespace MattEland.Jaimes.Agents.Services;

/// <summary>
/// Telemetry wrapper for Redis memory database that instruments all Redis operations with OpenTelemetry.
/// This allows tracking individual Redis queries and their durations in Aspire.
/// </summary>
public class TelemetryRedisMemoryDb(IMemoryDb inner) : IMemoryDb
{
    private static readonly ActivitySource ActivitySource = new("Jaimes.KernelMemory.Redis");
    
    private readonly IMemoryDb _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public async Task CreateIndexAsync(string index, int vectorSize, CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.CreateIndex", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.CreateIndex {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "create_index");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
            activity.SetTag("db.vector_size", vectorSize);
        }

        try
        {
            await _inner.CreateIndexAsync(index, vectorSize, cancellationToken);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
    }

    public async Task DeleteIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.DeleteIndex", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.DeleteIndex {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "delete_index");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
        }

        try
        {
            await _inner.DeleteIndexAsync(index, cancellationToken);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
    }

    public async IAsyncEnumerable<MemoryRecord> GetListAsync(
        string index,
        ICollection<MemoryFilter>? filters = null,
        int limit = 100,
        bool withEmbeddings = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.GetList", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.GetList {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "get_list");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
            activity.SetTag("db.limit", limit);
            activity.SetTag("db.with_embeddings", withEmbeddings);
            if (filters != null && filters.Count > 0)
            {
                activity.SetTag("db.filter_count", filters.Count);
            }
        }

        int count = 0;
        IAsyncEnumerable<MemoryRecord> enumerable = _inner.GetListAsync(index, filters, limit, withEmbeddings, cancellationToken);
        IAsyncEnumerator<MemoryRecord> enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                count++;
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
            if (activity != null)
            {
                activity.SetTag("db.result_count", count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
    }

    public async Task DeleteAsync(
        string index,
        MemoryRecord record,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.Delete", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.Delete {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "delete");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
            activity.SetTag("db.record_id", record.Id);
        }

        try
        {
            await _inner.DeleteAsync(index, record, cancellationToken);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
    }

    public async Task<string> UpsertAsync(
        string index,
        MemoryRecord record,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.Upsert", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.Upsert {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "upsert");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
            activity.SetTag("db.record_id", record.Id);
        }

        try
        {
            string result = await _inner.UpsertAsync(index, record, cancellationToken);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
    }

    public async IAsyncEnumerable<(MemoryRecord, double)> GetSimilarListAsync(
        string index,
        string text,
        ICollection<MemoryFilter>? filters = null,
        double minRelevance = 0,
        int limit = 100,
        bool withEmbeddings = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.GetSimilarList", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = $"Redis.GetSimilarList {index}";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "get_similar_list");
            activity.SetTag("db.name", "redis");
            activity.SetTag("db.index", index);
            activity.SetTag("db.limit", limit);
            activity.SetTag("db.min_relevance", minRelevance);
            activity.SetTag("db.with_embeddings", withEmbeddings);
            if (filters != null && filters.Count > 0)
            {
                activity.SetTag("db.filter_count", filters.Count);
            }
        }

        int count = 0;
        IAsyncEnumerable<(MemoryRecord, double)> enumerable = _inner.GetSimilarListAsync(
            index, text, filters, minRelevance, limit, withEmbeddings, cancellationToken);
        IAsyncEnumerator<(MemoryRecord, double)> enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                count++;
                yield return enumerator.Current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
            if (activity != null)
            {
                activity.SetTag("db.result_count", count);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
    }


    public async Task<IEnumerable<string>> GetIndexesAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = ActivitySource.StartActivity("Redis.GetIndexes", ActivityKind.Client);
        if (activity != null)
        {
            activity.DisplayName = "Redis.GetIndexes";
            activity.SetTag("db.system", "redis");
            activity.SetTag("db.operation", "get_indexes");
            activity.SetTag("db.name", "redis");
        }

        try
        {
            IEnumerable<string> result = await _inner.GetIndexesAsync(cancellationToken);
            
            if (activity != null)
            {
                activity.SetTag("db.index_count", result?.Count() ?? 0);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            
            return result ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            if (activity != null)
            {
                activity.SetTag("error", true);
                activity.SetTag("error.message", ex.Message);
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            throw;
        }
    }
}

