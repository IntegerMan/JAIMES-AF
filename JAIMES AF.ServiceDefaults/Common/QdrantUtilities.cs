using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Shared Qdrant / Embedding related helper methods to reduce duplication.
/// </summary>
public static class QdrantUtilities
{
    /// <summary>
    /// Generates a stable Qdrant point ID from a string using SHA256. Avoids collisions with earlier GetHashCode approach.
    /// Guarantees non-zero ID by falling through segments of the hash; uses ulong.MaxValue as a last-resort (statistically impossible path).
    /// </summary>
    public static ulong GeneratePointId(string pointId)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(pointId));
        ulong id = BitConverter.ToUInt64(hashBytes, 0);
        if (id != 0)
            return id;
        id = BitConverter.ToUInt64(hashBytes, 8);
        if (id != 0)
            return id;
        id = BitConverter.ToUInt64(hashBytes, 16);
        if (id != 0)
            return id;
        id = BitConverter.ToUInt64(hashBytes, 24);
        return id == 0 ? ulong.MaxValue : id;
    }

    /// <summary>
    /// Resolves embedding dimensions by generating a single sample embedding. Returns -1 if generator is null.
    /// Caches responsibility left to caller.
    /// </summary>
    public static async Task<int> ResolveEmbeddingDimensionsAsync(
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (embeddingGenerator is null)
        {
            logger.LogDebug("Embedding generator not available; cannot infer embedding dimensions.");
            return -1;
        }

        logger.LogDebug("Inferring embedding dimensions from model by generating a sample embedding");
        GeneratedEmbeddings<Embedding<float>> sample =
            await embeddingGenerator.GenerateAsync(["Sample text to determine embedding dimensions"],
                cancellationToken: cancellationToken);
        if (sample.Count == 0)
            throw new InvalidOperationException(
                "Failed to infer embedding dimensions: no embedding returned by generator");

        Embedding<float> first = sample[0];
        int dims = first.Vector.Length;
        if (dims <= 0) throw new InvalidOperationException("Embedding generator returned an invalid vector length");

        logger.LogInformation("Resolved embedding dimensions from model: {Dimensions}", dims);
        return dims;
    }
}