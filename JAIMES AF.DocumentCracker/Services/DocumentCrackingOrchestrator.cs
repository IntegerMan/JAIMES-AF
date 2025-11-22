using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MattEland.Jaimes.DocumentCracker.Models;
using MattEland.Jaimes.DocumentProcessing.Options;
using MattEland.Jaimes.DocumentProcessing.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RabbitMQ.Client;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MattEland.Jaimes.DocumentCracker.Services;

public class DocumentCrackingOrchestrator(
    ILogger<DocumentCrackingOrchestrator> logger,
    IDirectoryScanner directoryScanner,
    DocumentScanOptions options,
    IMongoClient mongoClient,
    RabbitMQ.Client.IConnection rabbitmqConnection)
{
    public async Task<DocumentCrackingSummary> CrackAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory configuration is required for document cracking.");
        }

        // Get database from connection string - the database name is "documents" as configured in AppHost
        IMongoDatabase mongoDatabase = mongoClient.GetDatabase("documents");
        IMongoCollection<CrackedDocument> collection = mongoDatabase.GetCollection<CrackedDocument>("crackedDocuments");

        DocumentCrackingSummary summary = new();

        List<string> directories = directoryScanner
            .GetSubdirectories(options.SourceDirectory)
            .ToList();
        directories.Insert(0, options.SourceDirectory);

        foreach (string directory in directories)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            string relativeDirectory = Path.GetRelativePath(options.SourceDirectory, directory);
            if (relativeDirectory == ".")
            {
                relativeDirectory = string.Empty;
            }

            IEnumerable<string> files = directoryScanner.GetFiles(directory, options.SupportedExtensions);
            foreach (string filePath in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                summary.TotalDiscovered++;

                if (!Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Skipping unsupported file: {FilePath}", filePath);
                    summary.SkippedUnsupported++;
                    continue;
                }

                try
                {
                    await CrackDocumentAsync(filePath, relativeDirectory, collection, cancellationToken);
                    summary.TotalCracked++;
                }
                catch (Exception ex)
                {
                    summary.TotalFailures++;
                    logger.LogError(ex, "Failed to crack document: {FilePath}", filePath);
                }
            }
        }

        return summary;
    }

    private async Task CrackDocumentAsync(string filePath, string relativeDirectory, IMongoCollection<CrackedDocument> collection, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting to crack document: {FilePath}", filePath);
        FileInfo fileInfo = new(filePath);
        (string contents, int pageCount) = ExtractPdfText(filePath);

        // Use UpdateOneAsync with upsert to avoid _id conflicts
        // This will update if the document exists (by FilePath) or insert if it doesn't
        FilterDefinition<CrackedDocument> filter = Builders<CrackedDocument>.Filter.Eq(d => d.FilePath, filePath);
        UpdateDefinition<CrackedDocument> update = Builders<CrackedDocument>.Update
            .Set(d => d.FilePath, filePath)
            .Set(d => d.RelativeDirectory, relativeDirectory)
            .Set(d => d.FileName, Path.GetFileName(filePath))
            .Set(d => d.Content, contents)
            .Set(d => d.CrackedAt, DateTime.UtcNow)
            .Set(d => d.FileSize, fileInfo.Length)
            .Set(d => d.PageCount, pageCount);
        
        UpdateOptions updateOptions = new() { IsUpsert = true };
        
        UpdateResult result = await collection.UpdateOneAsync(filter, update, updateOptions, cancellationToken);
        
        // Get the document ID after upsert
        // If it was an insert, use the UpsertedId; otherwise, query the document by FilePath
        string documentId = result.UpsertedId?.AsString ?? 
            (await collection.Find(filter).FirstOrDefaultAsync(cancellationToken))?.Id ?? string.Empty;
        
        logger.LogInformation("Cracked and saved to MongoDB: {FilePath} ({PageCount} pages, {FileSize} bytes)", 
            filePath, pageCount, fileInfo.Length);
        
        // Publish message to RabbitMQ
        PublishDocumentCrackedMessage(documentId, filePath, relativeDirectory, 
            Path.GetFileName(filePath), fileInfo.Length, pageCount);
    }
    
    private void PublishDocumentCrackedMessage(string documentId, string filePath, 
        string relativeDirectory, string fileName, long fileSize, int pageCount)
    {
        try
        {
            // Check if the connection is actually a RabbitMQ.Client.IConnection
            if (rabbitmqConnection is not RabbitMQ.Client.IConnection rmqConnection)
            {
                logger.LogWarning("RabbitMQ connection is not the expected type. Actual type: {Type}", 
                    rabbitmqConnection?.GetType().FullName ?? "null");
                return;
            }
            
            // Use reflection to call CreateModel - search on the interface type and base types
            Type connectionType = rmqConnection.GetType();
            MethodInfo? createModelMethod = connectionType.GetMethod("CreateModel", Type.EmptyTypes);
            
            // If not found on concrete type, try the interface
            if (createModelMethod == null)
            {
                createModelMethod = typeof(RabbitMQ.Client.IConnection).GetMethod("CreateModel", Type.EmptyTypes);
            }
            
            // If still not found, search in base types and interfaces
            if (createModelMethod == null)
            {
                Type? currentType = connectionType;
                while (currentType != null && createModelMethod == null)
                {
                    createModelMethod = currentType.GetMethod("CreateModel", 
                        BindingFlags.Public | BindingFlags.Instance, 
                        null, Type.EmptyTypes, null);
                    
                    if (createModelMethod == null)
                    {
                        foreach (Type interfaceType in currentType.GetInterfaces())
                        {
                            createModelMethod = interfaceType.GetMethod("CreateModel", Type.EmptyTypes);
                            if (createModelMethod != null) break;
                        }
                    }
                    
                    currentType = currentType.BaseType;
                    if (currentType == typeof(object)) break;
                }
            }
            
            if (createModelMethod == null)
            {
                logger.LogWarning("CreateModel method not found on RabbitMQ connection type: {Type}. Available methods: {Methods}", 
                    connectionType.FullName,
                    string.Join(", ", connectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.Name.Contains("Model"))
                        .Select(m => m.Name)));
                return;
            }
            
            var channelObj = createModelMethod.Invoke(rmqConnection, null);
            if (channelObj == null)
            {
                logger.LogWarning("CreateModel returned null");
                return;
            }
            
            // Use reflection to call methods on the channel
            var exchangeDeclareMethod = channelObj.GetType().GetMethod("ExchangeDeclare", 
                new[] { typeof(string), typeof(string), typeof(bool) });
            var queueDeclareMethod = channelObj.GetType().GetMethod("QueueDeclare", 
                new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(IDictionary<string, object>) });
            var queueBindMethod = channelObj.GetType().GetMethod("QueueBind", 
                new[] { typeof(string), typeof(string), typeof(string) });
            var createBasicPropertiesMethod = channelObj.GetType().GetMethod("CreateBasicProperties");
            var basicPublishMethod = channelObj.GetType().GetMethod("BasicPublish", 
                new[] { typeof(string), typeof(string), typeof(object), typeof(ReadOnlyMemory<byte>) });
            
            if (exchangeDeclareMethod == null || queueDeclareMethod == null || queueBindMethod == null || 
                createBasicPropertiesMethod == null || basicPublishMethod == null)
            {
                logger.LogWarning("Required RabbitMQ channel methods not found");
                return;
            }
            
            try
            {
                // Declare exchange and queue
                const string exchangeName = "document-events";
                const string queueName = "document-cracked";
                const string routingKey = "document.cracked";
                
                exchangeDeclareMethod.Invoke(channelObj, new object[] { exchangeName, "topic", true });
                IDictionary<string, object>? arguments = null;
                queueDeclareMethod.Invoke(channelObj, new object[] { queueName, true, false, false, arguments! });
                queueBindMethod.Invoke(channelObj, new object[] { queueName, exchangeName, routingKey });
                
                // Create message
                DocumentCrackedMessage message = new()
                {
                    DocumentId = documentId,
                    FilePath = filePath,
                    FileName = fileName,
                    RelativeDirectory = relativeDirectory,
                    FileSize = fileSize,
                    PageCount = pageCount,
                    CrackedAt = DateTime.UtcNow
                };
                
                // Serialize message
                string messageBody = JsonSerializer.Serialize(message);
                ReadOnlyMemory<byte> body = Encoding.UTF8.GetBytes(messageBody);
                
                // Publish message
                var properties = createBasicPropertiesMethod.Invoke(channelObj, null);
                if (properties == null)
                {
                    logger.LogWarning("CreateBasicProperties returned null");
                    return;
                }
                
                var persistentProp = properties.GetType().GetProperty("Persistent");
                var contentTypeProp = properties.GetType().GetProperty("ContentType");
                var timestampProp = properties.GetType().GetProperty("Timestamp");
                
                persistentProp?.SetValue(properties, true);
                contentTypeProp?.SetValue(properties, "application/json");
                if (timestampProp != null)
                {
                    var timestampValue = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    timestampProp.SetValue(properties, timestampValue);
                }
                
                basicPublishMethod.Invoke(channelObj, new object[] { exchangeName, routingKey, properties, body });
                
                logger.LogInformation("Successfully enqueued document cracked message to RabbitMQ. DocumentId: {DocumentId}, FilePath: {FilePath}, Queue: {QueueName}, Exchange: {ExchangeName}, RoutingKey: {RoutingKey}", 
                    documentId, filePath, queueName, exchangeName, routingKey);
            }
            finally
            {
                // Dispose channel if it implements IDisposable
                if (channelObj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the document cracking process
            logger.LogError(ex, "Failed to publish document cracked message to RabbitMQ: {FilePath}", filePath);
        }
    }

    private static (string content, int pageCount) ExtractPdfText(string filePath)
    {
        StringBuilder builder = new();
        using PdfDocument document = PdfDocument.Open(filePath);
        int pageCount = 0;
        
        foreach (Page page in document.GetPages())
        {
            pageCount++;
            builder.AppendLine($"--- Page {page.Number} ---");
            string pageText = ContentOrderTextExtractor.GetText(page);
            builder.AppendLine(pageText);
            builder.AppendLine();
        }

        return (builder.ToString(), pageCount);
    }

    public class DocumentCrackingSummary
    {
        public int TotalDiscovered { get; set; }
        public int TotalCracked { get; set; }
        public int TotalFailures { get; set; }
        public int SkippedUnsupported { get; set; }
    }
}


