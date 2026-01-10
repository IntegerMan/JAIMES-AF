using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MattEland.Jaimes.Services.Services;

/// <summary>
/// Service for classifying sentiment using ML.NET model loaded from database.
/// Lightweight service focused on prediction only (no training).
/// </summary>
public class SentimentClassificationService : ISentimentClassificationService, IAsyncDisposable
{
    private readonly ILogger<SentimentClassificationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MLContext _mlContext = new(seed: 0);
    private readonly double _confidenceThreshold = 0.6;
    private ObjectPool<PredictionEngine<SentimentData, SentimentPrediction>>? _predictionEnginePool;
    private ITransformer? _trainedModel;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SentimentClassificationService(
        ILogger<SentimentClassificationService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public async Task<(int Sentiment, double Confidence)> ClassifyAsync(
        string messageText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageText);

        // Lazy initialization on first use
        if (!_isInitialized)
        {
            await InitializeModelAsync(cancellationToken);
        }

        if (_predictionEnginePool == null)
        {
            _logger.LogWarning("Sentiment model not available, returning neutral");
            return (0, 0.0); // Return neutral with zero confidence if model unavailable
        }

        try
        {
            // Perform prediction
            (int sentiment, double confidence) = PredictSentiment(messageText);

            // Apply confidence threshold: if below threshold, set to neutral
            if (confidence < _confidenceThreshold)
            {
                _logger.LogDebug(
                    "Sentiment confidence {Confidence:P0} below threshold {Threshold:P0}, returning neutral",
                    confidence, _confidenceThreshold);
                sentiment = 0;
            }

            return (sentiment, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying sentiment for message text (length: {Length})", messageText.Length);
            return (0, 0.0); // Return neutral on error
        }
    }

    private async Task InitializeModelAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return; // Another thread already initialized
            }

            _logger.LogInformation("Initializing sentiment classification model from database");

            using IServiceScope scope = _scopeFactory.CreateScope();
            IClassificationModelService? classificationModelService =
                scope.ServiceProvider.GetService<IClassificationModelService>();

            if (classificationModelService == null)
            {
                _logger.LogWarning("IClassificationModelService not available, sentiment classification will not work");
                _isInitialized = true; // Mark as initialized to avoid retrying
                return;
            }

            // Get latest model from database
            ClassificationModelResponse? dbModel = await classificationModelService.GetLatestModelAsync(
                ClassificationModelTypes.SentimentClassification,
                cancellationToken);

            if (dbModel == null)
            {
                _logger.LogWarning("No sentiment model found in database");
                _isInitialized = true;
                return;
            }

            // Get model content (binary data)
            byte[]? modelContent = await classificationModelService.GetModelContentAsync(
                dbModel.Id,
                cancellationToken);

            if (modelContent == null || modelContent.Length == 0)
            {
                _logger.LogWarning("Failed to download model content from database");
                _isInitialized = true;
                return;
            }

            _logger.LogInformation("Loading sentiment model (ID: {ModelId}, created: {CreatedAt}, size: {Size} bytes)",
                dbModel.Id, dbModel.CreatedAt, modelContent.Length);

            // Load model from byte array
            using MemoryStream modelStream = new(modelContent);
            _trainedModel = _mlContext.Model.Load(modelStream, out _);

            // Create prediction engine pool for thread-safe predictions
            _predictionEnginePool = new DefaultObjectPool<PredictionEngine<SentimentData, SentimentPrediction>>(
                new PredictionEnginePooledObjectPolicy(_mlContext, _trainedModel));

            _isInitialized = true;
            _logger.LogInformation("Sentiment classification model initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize sentiment classification model");
            _isInitialized = true; // Mark as initialized to avoid infinite retries
        }
        finally
        {
            _initLock.Release();
        }
    }

    private (int Sentiment, double Confidence) PredictSentiment(string text)
    {
        if (_predictionEnginePool == null)
        {
            throw new InvalidOperationException("Model has not been loaded");
        }

        SentimentData input = new() { Text = text };

        // Get a prediction engine from the thread-safe pool
        PredictionEngine<SentimentData, SentimentPrediction> predictionEngine = _predictionEnginePool.Get();
        try
        {
            SentimentPrediction prediction = predictionEngine.Predict(input);

            // Get the highest confidence score
            float maxScore = prediction.Score != null && prediction.Score.Length > 0
                ? prediction.Score.Max()
                : 0f;

            // Map the predicted label to our sentiment values
            return prediction.PredictedLabel?.ToLowerInvariant().Trim() switch
            {
                "positive" => (1, maxScore),
                "negative" => (-1, maxScore),
                "neutral" => (0, maxScore),
                _ => (0, maxScore) // Default to neutral if label is unexpected
            };
        }
        finally
        {
            // Return the engine to the pool
            _predictionEnginePool.Return(predictionEngine);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _initLock?.Dispose();
        _predictionEnginePool = null;
        _trainedModel = null;
        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Input data structure for sentiment prediction.
    /// </summary>
    private class SentimentData
    {
        [LoadColumn(4)] // text column (matches training data structure)
        public string Text { get; set; } = string.Empty;

        [LoadColumn(5)] // sentiment column
        public string Sentiment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Prediction result from the sentiment model.
    /// </summary>
    private class SentimentPrediction
    {
        [ColumnName("PredictedLabel")] public string? PredictedLabel { get; set; }

        [ColumnName("Score")] public float[]? Score { get; set; }
    }

    /// <summary>
    /// Pooled object policy for PredictionEngine instances.
    /// This enables thread-safe reuse of PredictionEngine objects.
    /// </summary>
    private class PredictionEnginePooledObjectPolicy : IPooledObjectPolicy<PredictionEngine<SentimentData, SentimentPrediction>>
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;

        public PredictionEnginePooledObjectPolicy(MLContext mlContext, ITransformer model)
        {
            _mlContext = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public PredictionEngine<SentimentData, SentimentPrediction> Create()
        {
            return _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(_model);
        }

        public bool Return(PredictionEngine<SentimentData, SentimentPrediction> obj)
        {
            // Object can be returned to pool for reuse
            return true;
        }
    }
}
