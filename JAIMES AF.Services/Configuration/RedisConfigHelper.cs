using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryDb.Redis;

namespace MattEland.Jaimes.Services.Configuration;

/// <summary>
/// Centralized helper for creating RedisConfig instances with consistent tag field configuration.
/// </summary>
public static class RedisConfigHelper
{
    /// <summary>
    /// Creates a RedisConfig instance with the standard tag fields used by DocumentIndexer.
    /// IMPORTANT: All tag fields used when indexing documents MUST be declared here, or Redis will throw
    /// an "un-indexed tag field" error.
    /// </summary>
    /// <param name="connectionString">The Redis connection string (e.g., "localhost:6379" or "localhost:6379,password=xxx")</param>
    /// <returns>A configured RedisConfig instance</returns>
    public static RedisConfig CreateRedisConfig(string connectionString)
    {
        // Configure Redis with tag fields that Kernel Memory uses internally and that we use in our code
        // System tags: __part_n (document parts), collection (document organization)
        // Document tags: type, ruleset, fileName (used by DocumentIndexer)
        // See: https://github.com/microsoft/kernel-memory/discussions/735
        RedisConfig redisConfig = new("km-", new Dictionary<string, char?>
        {
            // System tags used by Kernel Memory internally
            { "__part_n", ',' },
            { "collection", ',' },
            // Document tags used by DocumentIndexer
            { "type", ',' },
            { "ruleset", ',' },
            { "fileName", ',' }
        });
        redisConfig.ConnectionString = connectionString;
        
        return redisConfig;
    }
}

