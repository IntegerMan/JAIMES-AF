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
                    // Kernel Memory keys follow patterns like:
                    // km-{index}-doc-{documentId} - Document metadata
                    // km-{index}-part-{partNumber}-{documentId} - Document parts
                    // km-{index}-{documentId} - Direct document reference
                    if (keyString.StartsWith("km-", StringComparison.OrdinalIgnoreCase))
                    {
                        string suffix = keyString.Substring(3); // Remove "km-" prefix
                        
                        // Look for known Kernel Memory key patterns to find where index name ends
                        // Kernel Memory keys follow patterns like:
                        // km-{index}-doc-{documentId} - Document metadata
                        // km-{index}-part-{partNumber}-{documentId} - Document parts
                        // km-{index}-{documentId} - Direct document reference (less common)
                        int docIndex = suffix.IndexOf("-doc-", StringComparison.OrdinalIgnoreCase);
                        int partIndex = suffix.IndexOf("-part-", StringComparison.OrdinalIgnoreCase);
                        
                        string? indexName = null;
                        if (docIndex > 0)
                        {
                            indexName = suffix.Substring(0, docIndex);
                        }
                        else if (partIndex > 0)
                        {
                            indexName = suffix.Substring(0, partIndex);
                        }
                        // For pattern 3 (km-{index}-{documentId}), we skip these keys because
                        // we can't reliably determine where the index name ends and the document ID begins
                        // without knowing the document ID structure. Most keys should follow patterns 1 or 2.
                        
                        if (!string.IsNullOrWhiteSpace(indexName))
                        {
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

    public async Task<DocumentListResponse> ListDocumentsAsync(string? indexName, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing documents for index: {IndexName}, page: {Page}, pageSize: {PageSize}", indexName ?? "all", page, pageSize);

        try
        {
            List<IndexedDocumentInfo> allDocuments = new();

            if (string.IsNullOrWhiteSpace(indexName))
            {
                // List documents from all indexes
                IndexListResponse indexResponse = await ListIndexesAsync(cancellationToken);
                
                foreach (string index in indexResponse.Indexes)
                {
                    await AddDocumentsFromIndexAsync(index, allDocuments, cancellationToken);
                }
            }
            else
            {
                // List documents from specific index
                await AddDocumentsFromIndexAsync(indexName, allDocuments, cancellationToken);
            }

            int totalCount = allDocuments.Count;
            int skip = (page - 1) * pageSize;
            IndexedDocumentInfo[] pagedDocuments = allDocuments
                .OrderBy(d => d.DocumentId)
                .Skip(skip)
                .Take(pageSize)
                .ToArray();

            _logger.LogInformation("Found {TotalCount} documents in index: {IndexName}, returning page {Page} with {Count} documents", 
                totalCount, indexName ?? "all", page, pagedDocuments.Length);

            return new DocumentListResponse
            {
                IndexName = indexName ?? "all",
                Documents = pagedDocuments,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
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
                            // Read tags from part keys - Kernel Memory stores document tags in part keys
                            // Look for any part key that contains this document ID
                            string partPattern = $"km-{indexName}-part-*";
                            int partCursor = 0;
                            bool foundTags = false;
                            
                            do
                            {
                                RedisResult partResult = await db.ExecuteAsync("SCAN", partCursor.ToString(), "MATCH", partPattern, "COUNT", "100");
                                RedisResult[] partScanResult = (RedisResult[])partResult!;
                                partCursor = int.Parse(partScanResult[0].ToString()!);
                                RedisResult partKeysResult = partScanResult[1];
                                RedisValue[] partKeys = (RedisValue[])partKeysResult!;
                                
                                foreach (RedisValue partKeyValue in partKeys)
                                {
                                    string partKey = partKeyValue.ToString()!;
                                    
                                    // Check if this part key belongs to our document
                                    // Part keys have format: km-{index}-part-{partNumber}-{documentId}
                                    if (partKey.EndsWith($"-{documentId}", StringComparison.OrdinalIgnoreCase))
                                    {
                                        RedisType partKeyType = await db.KeyTypeAsync(partKey);
                                        
                                        if (partKeyType == RedisType.Hash)
                                        {
                                            HashEntry[] partFields = await db.HashGetAllAsync(partKey);
                                            foreach (HashEntry field in partFields)
                                            {
                                                string fieldName = field.Name.ToString();
                                                string fieldValue = field.Value.ToString();
                                                
                                                // Kernel Memory stores tags as hash fields in part keys
                                                // Tags like sourcePath, fileName are stored directly
                                                if (fieldName == "sourcePath" || fieldName == "fileName" || 
                                                    fieldName == "rulesetId" || fieldName == "ruleId" || fieldName == "title")
                                                {
                                                    tags[fieldName] = fieldValue;
                                                }
                                            }
                                            
                                            if (tags.Count > 0)
                                            {
                                                foundTags = true;
                                                break; // Found tags, no need to check more part keys
                                            }
                                        }
                                    }
                                }
                                
                                if (foundTags)
                                {
                                    break;
                                }
                            } while (partCursor != 0);
                            
                            // Set status to Completed if we found the document (it's indexed)
                            status = "Completed";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error reading metadata for document {DocumentId} in index {IndexName}", documentId, indexName);
                            // Default to Completed since we found the document key
                            status = "Completed";
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

