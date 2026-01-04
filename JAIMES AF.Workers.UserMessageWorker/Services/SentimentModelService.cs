using MattEland.Jaimes.Workers.UserMessageWorker.Options;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Services;

/// <summary>
/// Service for training and loading sentiment analysis models using ML.NET AutoML.
/// Training data sourced from: https://www.kaggle.com/datasets/mdismielhossenabir/sentiment-analysis
/// </summary>
public class SentimentModelService(
    ILogger<SentimentModelService> logger,
    IClassificationModelService? classificationModelService = null,
    IDbContextFactory<JaimesDbContext>? contextFactory = null,
    IOptions<SentimentAnalysisOptions>? sentimentOptions = null)
{
    private const string ModelFileName = "Result/SentimentModel.zip";
    private const string TrainingDataFileName = "sentiment_analysis.csv";
    private readonly ILogger<SentimentModelService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private ObjectPool<PredictionEngine<SentimentData, SentimentPrediction>>? _predictionEnginePool;
    private ITransformer? _trainedModel;
    private readonly MLContext _mlContext = new(seed: 0);

    /// <summary>
    /// Loads the trained model if it exists, otherwise trains a new model.
    /// Checks the database first for the latest model.
    /// </summary>
    public async Task LoadOrTrainModelAsync(CancellationToken cancellationToken = default)
    {
        string modelPath = Path.Combine(AppContext.BaseDirectory, ModelFileName);
        string trainingDataPath = Path.Combine(AppContext.BaseDirectory, TrainingDataFileName);

        // First, check database for latest model
        if (classificationModelService != null)
        {
            bool downloadedFromDb = await TryDownloadModelFromDatabaseAsync(modelPath, cancellationToken);
            if (downloadedFromDb)
            {
                return;
            }
        }

        // Fall back to local model or training
        if (File.Exists(modelPath))
        {
            try
            {
                _logger.LogInformation("Loading existing sentiment model from {ModelPath}", modelPath);
                await LoadModelAsync(modelPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load sentiment model from {ModelPath}. File may be corrupted. Deleting and training new model.",
                    modelPath);
                try
                {
                    File.Delete(modelPath);
                }
                catch
                {
                    /* Ignore deletion errors */
                }

                await TrainAndSaveModelAsync(trainingDataPath, modelPath, cancellationToken);
            }
        }
        else
        {
            _logger.LogInformation("Model not found. Training new sentiment model from {TrainingDataPath}",
                trainingDataPath);
            await TrainAndSaveModelAsync(trainingDataPath, modelPath, cancellationToken);
        }
    }

    /// <summary>
    /// Attempts to download the model from the database if it is newer than the local copy.
    /// </summary>
    private async Task<bool> TryDownloadModelFromDatabaseAsync(string localModelPath,
        CancellationToken cancellationToken)
    {
        try
        {
            ClassificationModelResponse? dbModel = await classificationModelService!.GetLatestModelAsync(
                ClassificationModelTypes.SentimentClassification,
                cancellationToken);

            if (dbModel == null)
            {
                _logger.LogInformation("No classification model found in database");
                return false;
            }

            // Check if local model exists and compare timestamps
            if (File.Exists(localModelPath))
            {
                FileInfo localFile = new(localModelPath);
                if (localFile.LastWriteTimeUtc >= dbModel.CreatedAt)
                {
                    _logger.LogInformation(
                        "Local model ({LocalTime:u}) is newer or same as database model ({DbTime:u}). Using local model.",
                        localFile.LastWriteTimeUtc,
                        dbModel.CreatedAt);
                    await LoadModelAsync(localModelPath, cancellationToken);
                    return true;
                }

                _logger.LogInformation(
                    "Database model ({DbTime:u}) is newer than local model ({LocalTime:u}). Downloading from database.",
                    dbModel.CreatedAt,
                    localFile.LastWriteTimeUtc);
            }
            else
            {
                _logger.LogInformation("Downloading classification model from database (no local model exists)");
            }

            // Download model content from database using the model ID
            byte[]? modelContent = await classificationModelService.GetModelContentAsync(
                dbModel.Id,
                cancellationToken);

            if (modelContent == null || modelContent.Length == 0)
            {
                _logger.LogWarning("Failed to download model content from database");
                return false;
            }

            // Save to local path
            string? modelDir = Path.GetDirectoryName(localModelPath);
            if (!string.IsNullOrEmpty(modelDir) && !Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }

            await File.WriteAllBytesAsync(localModelPath, modelContent, cancellationToken);
            _logger.LogInformation("Downloaded classification model to {ModelPath} ({Size} bytes)",
                localModelPath,
                modelContent.Length);

            // Load the downloaded model
            await LoadModelAsync(localModelPath, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to download or load model from database. Falling back to local/fresh model.");
            return false;
        }
    }


    /// <summary>
    /// Predicts the sentiment of the given text.
    /// Returns: 1 for positive, -1 for negative, 0 for neutral
    /// </summary>
    public (int Prediction, double Confidence) PredictSentiment(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (_predictionEnginePool == null)
        {
            throw new InvalidOperationException("Model has not been loaded. Call LoadOrTrainModelAsync first.");
        }

        SentimentData input = new() {Text = text};

        // Get a prediction engine from the thread-safe pool
        PredictionEngine<SentimentData, SentimentPrediction> predictionEngine = _predictionEnginePool.Get();
        try
        {
            SentimentPrediction prediction = predictionEngine.Predict(input);

            // Get the highest confidence score
            // Handle null or empty array to avoid InvalidOperationException from Max() on empty sequence
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

    /// <summary>
    /// Analyzes sentiment with confidence threshold applied.
    /// Returns the final sentiment value (after applying threshold) and the confidence score.
    /// </summary>
    public (int FinalSentiment, double Confidence) AnalyzeSentimentWithThreshold(string text,
        double confidenceThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        (int sentiment, double confidence) = PredictSentiment(text);

        // Apply confidence threshold: if below threshold, set to neutral
        if (confidence < confidenceThreshold)
        {
            sentiment = 0; // Neutral if below threshold
        }

        return (sentiment, confidence);
    }

    /// <summary>
    /// Reclassifies the sentiment of all user messages in the Messages table.
    /// </summary>
    public async Task ReclassifyAllUserMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (contextFactory == null)
        {
            throw new InvalidOperationException(
                "DbContextFactory is required for reclassification. Ensure it is provided in the constructor.");
        }

        if (sentimentOptions == null)
        {
            throw new InvalidOperationException(
                "SentimentAnalysisOptions is required for reclassification. Ensure it is provided in the constructor.");
        }

        _logger.LogInformation("Starting reclassification of all user messages");

        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Query all user messages (messages where PlayerId is not null) and include their sentiment
        List<Message> userMessages = await context.Messages
            .Include(m => m.MessageSentiment)
            .Where(m => m.PlayerId != null && !string.IsNullOrWhiteSpace(m.Text))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} user messages to reclassify", userMessages.Count);

        if (userMessages.Count == 0)
        {
            _logger.LogInformation("No user messages found to reclassify");
            return;
        }

        double confidenceThreshold = sentimentOptions.Value.ConfidenceThreshold;
        int processedCount = 0;
        int updatedCount = 0;
        int errorCount = 0;
        DateTime now = DateTime.UtcNow;

        foreach (Message message in userMessages)
        {
            try
            {
                (int finalSentiment, double confidence) =
                    AnalyzeSentimentWithThreshold(message.Text, confidenceThreshold);

                // Check if sentiment needs to be created or updated
                if (message.MessageSentiment == null)
                {
                    // Create new sentiment record
                    MessageSentiment newSentiment = new()
                    {
                        MessageId = message.Id,
                        Sentiment = finalSentiment,
                        Confidence = confidence,
                        CreatedAt = now,
                        UpdatedAt = now,
                        SentimentSource = SentimentSource.Model
                    };
                    context.MessageSentiments.Add(newSentiment);
                    updatedCount++;
                }
                else if (message.MessageSentiment.SentimentSource == SentimentSource.Player)
                {
                    // Skip if manually set by player
                    _logger.LogDebug(
                        "Skipping reclassification for message {MessageId} as it was manually set by a player",
                        message.Id);
                }
                else if (message.MessageSentiment.Sentiment != finalSentiment ||
                         message.MessageSentiment.Confidence != confidence)
                {
                    // Update existing sentiment if changed
                    message.MessageSentiment.Sentiment = finalSentiment;
                    message.MessageSentiment.Confidence = confidence;
                    message.MessageSentiment.UpdatedAt = now;
                    message.MessageSentiment.SentimentSource = SentimentSource.Model;
                    updatedCount++;
                }

                processedCount++;

                // Log progress every 100 messages
                if (processedCount % 100 == 0)
                {
                    _logger.LogInformation(
                        "Reclassification progress: {Processed}/{Total} messages processed, {Updated} updated",
                        processedCount,
                        userMessages.Count,
                        updatedCount);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Failed to reclassify sentiment for message {MessageId}", message.Id);
            }
        }

        // Save all changes in a single transaction
        if (updatedCount > 0)
        {
            _logger.LogInformation("Saving {UpdatedCount} sentiment updates to database", updatedCount);
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Reclassification completed: {Processed} processed, {Updated} updated, {Errors} errors",
            processedCount,
            updatedCount,
            errorCount);
    }

    private async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
            {
                _trainedModel = _mlContext.Model.Load(modelPath, out _);

                // Create a thread-safe prediction engine pool using ObjectPool
                DefaultObjectPoolProvider poolProvider = new();
                _predictionEnginePool =
                    poolProvider.Create(new PredictionEnginePooledObjectPolicy(_mlContext, _trainedModel));

                _logger.LogInformation("Successfully loaded sentiment model from {ModelPath} with thread-safe pool",
                    modelPath);
            },
            cancellationToken);
    }

    private async Task TrainAndSaveModelAsync(string trainingDataPath,
        string modelPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(trainingDataPath))
        {
            throw new FileNotFoundException($"Training data file not found: {trainingDataPath}");
        }

        await Task.Run(() =>
            {
                _logger.LogInformation("Loading training data from {TrainingDataPath}", trainingDataPath);
                IDataView trainingData = _mlContext.Data.LoadFromTextFile<SentimentData>(
                    trainingDataPath,
                    separatorChar: ',',
                    hasHeader: true,
                    allowQuoting: true,
                    trimWhitespace: true);

                // Configure AutoML experiment
                MulticlassExperimentSettings experimentSettings = new()
                {
                    MaxExperimentTimeInSeconds = 60, // 1 minute for training
                    OptimizingMetric = MulticlassClassificationMetric.MacroAccuracy
                };

                // Verify the data schema
                var schema = trainingData.Schema;
                _logger.LogInformation("Training data schema - Column count: {ColumnCount}", schema.Count);
                foreach (var column in schema)
                {
                    _logger.LogDebug("Column: {ColumnName}, Type: {ColumnType}", column.Name, column.Type);
                }

                // Verify we have the expected columns
                if (schema.GetColumnOrNull(nameof(SentimentData.Text)) == null)
                {
                    throw new InvalidOperationException(
                        $"Training data does not contain required column: {nameof(SentimentData.Text)}");
                }

                if (schema.GetColumnOrNull(nameof(SentimentData.Sentiment)) == null)
                {
                    throw new InvalidOperationException(
                        $"Training data does not contain required column: {nameof(SentimentData.Sentiment)}");
                }

                // Preprocess: Featurize the text column into a Features vector
                // This is necessary because AutoML needs numeric features
                _logger.LogInformation("Featurizing text data...");
                IEstimator<ITransformer> textFeaturizer = _mlContext.Transforms.Text
                    .FeaturizeText("Features", nameof(SentimentData.Text));

                ITransformer textTransformer = textFeaturizer.Fit(trainingData);
                IDataView featurizedData = textTransformer.Transform(trainingData);

                // Update column information - AutoML will automatically use the Features column
                ColumnInformation featurizedColumnInfo = new()
                {
                    LabelColumnName = nameof(SentimentData.Sentiment)
                };

                _logger.LogInformation(
                    "Starting AutoML experiment for sentiment classification with timeout of {Timeout} seconds...",
                    experimentSettings.MaxExperimentTimeInSeconds);
                ExperimentResult<MulticlassClassificationMetrics> experimentResult = _mlContext.Auto()
                    .CreateMulticlassClassificationExperiment(experimentSettings)
                    .Execute(featurizedData, featurizedColumnInfo);

                // Combine the text featurizer with the AutoML model
                ITransformer combinedModel = textTransformer.Append(experimentResult.BestRun.Model);

                _logger.LogInformation(
                    "AutoML experiment completed. Best run: {TrainerName}, Accuracy: {Accuracy}",
                    experimentResult.BestRun.TrainerName,
                    experimentResult.BestRun.ValidationMetrics?.MacroAccuracy ?? 0);

                // Save the combined model (text featurizer + AutoML model)
                string modelDirectory = Path.GetDirectoryName(modelPath) ??
                                        throw new InvalidOperationException("Invalid model path");
                Directory.CreateDirectory(modelDirectory);
                _mlContext.Model.Save(combinedModel, trainingData.Schema, modelPath);
                _logger.LogInformation("Trained model saved to {ModelPath}", modelPath);

                // Store the model for pool creation
                _trainedModel = combinedModel;

                // Create a thread-safe prediction engine pool for immediate use (using combined model)
                DefaultObjectPoolProvider poolProvider = new();
                _predictionEnginePool =
                    poolProvider.Create(new PredictionEnginePooledObjectPolicy(_mlContext, _trainedModel));
            },
            cancellationToken);
    }

    /// <summary>
    /// Input data structure for sentiment analysis.
    /// CSV columns: Year, Month, Day, Time of Tweet, text, sentiment, Platform
    /// </summary>
    public class SentimentData
    {
        [LoadColumn(4)] // text column
        public string Text { get; set; } = string.Empty;

        [LoadColumn(5)] // sentiment column
        public string Sentiment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Prediction result from the sentiment model.
    /// </summary>
    public class SentimentPrediction
    {
        [ColumnName("PredictedLabel")] public string? PredictedLabel { get; set; }

        [ColumnName("Score")] public float[]? Score { get; set; }
    }

    /// <summary>
    /// Pooled object policy for PredictionEngine instances.
    /// This enables thread-safe reuse of PredictionEngine objects.
    /// </summary>
    private class
        PredictionEnginePooledObjectPolicy : IPooledObjectPolicy<PredictionEngine<SentimentData, SentimentPrediction>>
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
            // Always return true to indicate the object can be reused
            // PredictionEngine instances are reusable and don't need to be disposed when returned to the pool
            return true;
        }
    }
}

