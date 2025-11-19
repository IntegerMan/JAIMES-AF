using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Agents.Services;

public class DocumentListService : IDocumentListService
{
    private readonly ILogger<DocumentListService> _logger;
    private readonly VectorDbOptions _vectorDbOptions;
    private IConnectionMultiplexer? _redisConnection;

    public DocumentListService(
        ILogger<DocumentListService> logger,
        VectorDbOptions vectorDbOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vectorDbOptions = vectorDbOptions ?? throw new ArgumentNullException(nameof(vectorDbOptions));

        _logger.LogInformation("DocumentListService initialized with Redis");
    }

    public async Task<IndexListResponse> ListIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing all indexes from Redis");

        try
        {
            IConnectionMultiplexer redis = await GetRedisConnectionAsync(cancellationToken);
            IDatabase db = redis.GetDatabase();
            
            // Scan for all Kernel Memory keys (km-*) to extract unique index names
            HashSet<string> indexes = new();
            string keyPattern = "km-*";
            
            int cursor = 0;
            do
            {
                RedisResult result = await db.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", keyPattern, "COUNT", "100");
                RedisResult[] scanResult = (RedisResult[])result!;
                cursor = int.Parse(scanResult[0].ToString()!);
                RedisResult keysResult = scanResult[1];
                RedisValue[] keys = (RedisValue[])keysResult!;
                
                foreach (RedisValue key in keys)
                {
                    string keyString = key.ToString();
                    // Extract index name from key pattern: km-{index}-...
                    if (keyString.StartsWith("km-", StringComparison.OrdinalIgnoreCase))
                    {
                        string suffix = keyString.Substring(3); // Remove "km-" prefix
                        int dashIndex = suffix.IndexOf('-');
                        if (dashIndex > 0)
                        {
                            string indexName = suffix.Substring(0, dashIndex);
                            indexes.Add(indexName);
                        }
                    }
                }
            } while (cursor != 0);
            
            List<string> indexList = indexes.OrderBy(i => i).ToList();
            _logger.LogInformation("Found {Count} indexes", indexList.Count);
            
            return new IndexListResponse
            {
                Indexes = indexList.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing indexes");
            throw;
        }
    }

    public async Task<DocumentListResponse> ListDocumentsAsync(string? indexName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing documents for index: {IndexName}", indexName ?? "all");

        try
        {
            List<IndexedDocumentInfo> documents = new();

            if (string.IsNullOrWhiteSpace(indexName))
            {
                // List documents from all indexes
                IndexListResponse indexResponse = await ListIndexesAsync(cancellationToken);
                
                foreach (string index in indexResponse.Indexes)
                {
                    await AddDocumentsFromIndexAsync(index, documents, cancellationToken);
                }
            }
            else
            {
                // List documents from specific index
                await AddDocumentsFromIndexAsync(indexName, documents, cancellationToken);
            }

            _logger.LogInformation("Found {Count} documents in index: {IndexName}", documents.Count, indexName ?? "all");

            return new DocumentListResponse
            {
                IndexName = indexName ?? "all",
                Documents = documents.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents for index: {IndexName}", indexName);
            throw;
        }
    }

    private async Task AddDocumentsFromIndexAsync(string indexName, List<IndexedDocumentInfo> documents, CancellationToken cancellationToken)
    {
        try
        {
            // Connect directly to Redis to scan for document keys
            // Kernel Memory stores documents with keys like "km-{index}-doc-{documentId}" or similar patterns
            _logger.LogInformation("Listing documents from index {IndexName} by scanning Redis keys", indexName);
            
            IConnectionMultiplexer redis = await GetRedisConnectionAsync(cancellationToken);
            IDatabase db = redis.GetDatabase();
            
            // Kernel Memory uses "km-" prefix, and documents are stored with patterns like:
            // km-{index}-doc-{documentId} or km-{index}-{documentId}
            // We'll scan for all keys matching the index pattern
            string keyPattern = $"km-{indexName}-*";
            HashSet<string> seenDocumentIds = new();
            
            // Use SCAN to iterate through keys without blocking Redis
            int cursor = 0;
            do
            {
                RedisResult result = await db.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", keyPattern, "COUNT", "100");
                RedisResult[] scanResult = (RedisResult[])result!;
                cursor = int.Parse(scanResult[0].ToString()!);
                RedisResult keysResult = scanResult[1];
                RedisValue[] keys = (RedisValue[])keysResult!;
                
                foreach (RedisValue key in keys)
                {
                    string keyString = key.ToString();
                    // Extract document ID from key pattern
                    // Keys are typically: km-{index}-doc-{documentId} or km-{index}-{documentId}
                    string? documentId = ExtractDocumentIdFromKey(keyString, indexName);
                    
                    if (!string.IsNullOrWhiteSpace(documentId) && !seenDocumentIds.Contains(documentId))
                    {
                        seenDocumentIds.Add(documentId);
                        
                        // Get document metadata directly from Redis
                        Dictionary<string, string> tags = new();
                        DateTime? lastUpdate = null;
                        string status = "Unknown";
                        
                        try
                        {
                            // Try to read document metadata from Redis
                            // Kernel Memory stores document metadata in keys like km-{index}-doc-{documentId}
                            string docKey = $"km-{indexName}-doc-{documentId}";
                            
                            // Check if the key exists and what type it is
                            RedisType keyType = await db.KeyTypeAsync(docKey);
                            
                            if (keyType == RedisType.Hash)
                            {
                                // Read hash fields (tags and metadata)
                                HashEntry[] hashFields = await db.HashGetAllAsync(docKey);
                                foreach (HashEntry field in hashFields)
                                {
                                    string fieldName = field.Name.ToString();
                                    string fieldValue = field.Value.ToString();
                                    
                                    // Extract tags (Kernel Memory stores tags with specific field names)
                                    if (fieldName.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string tagName = fieldName.Substring(4); // Remove "tag:" prefix
                                        tags[tagName] = fieldValue;
                                    }
                                    else if (fieldName.Equals("lastUpdate", StringComparison.OrdinalIgnoreCase) ||
                                             fieldName.Equals("updated", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Try to parse timestamp
                                        if (long.TryParse(fieldValue, out long timestamp))
                                        {
                                            lastUpdate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                                        }
                                        else if (DateTime.TryParse(fieldValue, out DateTime parsedDate))
                                        {
                                            lastUpdate = parsedDate;
                                        }
                                    }
                                    else if (fieldName.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                                             fieldName.Equals("completed", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (bool.TryParse(fieldValue, out bool completed))
                                        {
                                            status = completed ? "Completed" : "Processing";
                                        }
                                        else
                                        {
                                            status = fieldValue;
                                        }
                                    }
                                }
                            }
                            else if (keyType == RedisType.String)
                            {
                                // Try to parse as JSON
                                string jsonValue = await db.StringGetAsync(docKey);
                                if (!string.IsNullOrWhiteSpace(jsonValue))
                                {
                                    try
                                    {
                                        using JsonDocument doc = JsonDocument.Parse(jsonValue);
                                        JsonElement root = doc.RootElement;
                                        
                                        // Extract tags from JSON
                                        if (root.TryGetProperty("tags", out JsonElement tagsElement))
                                        {
                                            if (tagsElement.ValueKind == JsonValueKind.Object)
                                            {
                                                foreach (JsonProperty prop in tagsElement.EnumerateObject())
                                                {
                                                    string? tagValue = prop.Value.GetString();
                                                    if (!string.IsNullOrWhiteSpace(tagValue))
                                                    {
                                                        tags[prop.Name] = tagValue;
                                                    }
                                                }
                                            }
                                        }
                                        
                                        // Extract lastUpdate
                                        if (root.TryGetProperty("lastUpdate", out JsonElement lastUpdateElement))
                                        {
                                            if (lastUpdateElement.ValueKind == JsonValueKind.String &&
                                                DateTime.TryParse(lastUpdateElement.GetString(), out DateTime parsedDate))
                                            {
                                                lastUpdate = parsedDate;
                                            }
                                        }
                                        
                                        // Extract status
                                        if (root.TryGetProperty("status", out JsonElement statusElement))
                                        {
                                            status = statusElement.GetString() ?? "Unknown";
                                        }
                                        else if (root.TryGetProperty("completed", out JsonElement completedElement))
                                        {
                                            if (completedElement.ValueKind == JsonValueKind.True)
                                            {
                                                status = "Completed";
                                            }
                                            else if (completedElement.ValueKind == JsonValueKind.False)
                                            {
                                                status = "Processing";
                                            }
                                        }
                                    }
                                    catch (JsonException)
                                    {
                                        // Not JSON, ignore
                                    }
                                }
                            }
                            
                            // Also check for tags stored separately in Redis search indexes
                            // Kernel Memory Redis uses tag fields that might be indexed separately
                            // We can try to read tag values from the document key pattern
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading metadata for document {DocumentId} in index {IndexName}", documentId, indexName);
                        }

                        documents.Add(new IndexedDocumentInfo
                        {
                            DocumentId = documentId,
                            Index = indexName,
                            Tags = tags,
                            LastUpdate = lastUpdate,
                            Status = status
                        });
                    }
                }
            } while (cursor != 0);
            
            _logger.LogInformation("Found {Count} unique documents in index {IndexName} by scanning Redis keys", seenDocumentIds.Count, indexName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing documents from index: {IndexName}", indexName);
            // Continue with other indexes even if one fails
        }
    }

    private async Task<IConnectionMultiplexer> GetRedisConnectionAsync(CancellationToken cancellationToken)
    {
        if (_redisConnection != null && _redisConnection.IsConnected)
        {
            return _redisConnection;
        }

        // Parse connection string - handle formats like "localhost:6379" or "localhost:6379,password=xxx"
        string connectionString = _vectorDbOptions.ConnectionString;
        
        // If it's in old format (Data Source=...), use default
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "localhost:6379";
        }
        
        // Convert simple format to StackExchange.Redis format if needed
        if (!connectionString.Contains("://") && !connectionString.Contains(","))
        {
            // Simple format like "localhost:6379" - StackExchange.Redis can handle this directly
            connectionString = connectionString;
        }

        _redisConnection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        return _redisConnection;
    }

    private static string? ExtractDocumentIdFromKey(string key, string indexName)
    {
        // Kernel Memory Redis keys follow patterns like:
        // km-{index}-doc-{documentId} - Document metadata
        // km-{index}-part-{partNumber}-{documentId} - Document parts
        // km-{index}-{documentId} - Direct document reference
        // We need to extract the document ID from these patterns
        
        string prefix = $"km-{indexName}-";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string suffix = key.Substring(prefix.Length);
        
        // Pattern 1: km-{index}-doc-{documentId} - Document metadata key
        if (suffix.StartsWith("doc-", StringComparison.OrdinalIgnoreCase))
        {
            return suffix.Substring(4); // Remove "doc-" prefix
        }
        
        // Pattern 2: km-{index}-part-{partNumber}-{documentId} - Document part key
        // The document ID is the last segment after splitting by dashes
        if (suffix.StartsWith("part-", StringComparison.OrdinalIgnoreCase))
        {
            // Format: part-{partNumber}-{documentId}
            // Extract document ID (last segment after removing "part-{partNumber}-")
            string[] parts = suffix.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                // parts[0] = "part", parts[1] = partNumber, parts[2+] = documentId (may contain dashes)
                // Join everything after the part number as the document ID
                return string.Join("-", parts.Skip(2));
            }
            return null;
        }
        
        // Pattern 3: km-{index}-{documentId} (direct) - Simple document key
        // If there are no special prefixes, the suffix is the document ID
        return suffix;
    }
}

