// Training data source: https://www.kaggle.com/datasets/mdismielhossenabir/sentiment-analysis
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Services;

/// <summary>
/// Service for training and loading sentiment analysis models using ML.NET AutoML.
/// Training data sourced from: https://www.kaggle.com/datasets/mdismielhossenabir/sentiment-analysis
/// </summary>
public class SentimentModelService(ILogger<SentimentModelService> logger)
{
    private const string ModelFileName = "Result/SentimentModel.zip";
    private const string TrainingDataFileName = "sentiment_analysis.csv";
    private readonly ILogger<SentimentModelService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private PredictionEngine<SentimentData, SentimentPrediction>? _predictionEngine;
    private readonly MLContext _mlContext = new(seed: 0);

    /// <summary>
    /// Loads the trained model if it exists, otherwise trains a new model.
    /// </summary>
    public async Task LoadOrTrainModelAsync(CancellationToken cancellationToken = default)
    {
        string modelPath = Path.Combine(AppContext.BaseDirectory, ModelFileName);
        string trainingDataPath = Path.Combine(AppContext.BaseDirectory, TrainingDataFileName);

        if (File.Exists(modelPath))
        {
            _logger.LogInformation("Loading existing sentiment model from {ModelPath}", modelPath);
            await LoadModelAsync(modelPath, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Model not found. Training new sentiment model from {TrainingDataPath}", trainingDataPath);
            await TrainAndSaveModelAsync(trainingDataPath, modelPath, cancellationToken);
        }
    }

    /// <summary>
    /// Predicts the sentiment of the given text.
    /// Returns: 1 for positive, -1 for negative, 0 for neutral
    /// </summary>
    public (int Prediction, double Confidence) PredictSentiment(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (_predictionEngine == null)
        {
            throw new InvalidOperationException("Model has not been loaded. Call LoadOrTrainModelAsync first.");
        }

        SentimentData input = new() { Text = text };
        SentimentPrediction prediction = _predictionEngine.Predict(input);

        // Get the highest confidence score
        float maxScore = prediction.Score?.Max() ?? 0f;


        // Map the predicted label to our sentiment values
        return prediction.PredictedLabel?.ToLowerInvariant().Trim() switch
        {
            "positive" => (1, maxScore),
            "negative" => (-1, maxScore),
            "neutral" => (0, maxScore),
            _ => (0, maxScore) // Default to neutral if label is unexpected
        };
    }

    private async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            ITransformer trainedModel = _mlContext.Model.Load(modelPath, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(trainedModel);
            _logger.LogInformation("Successfully loaded sentiment model from {ModelPath}", modelPath);
        }, cancellationToken);
    }

    private async Task TrainAndSaveModelAsync(string trainingDataPath, string modelPath, CancellationToken cancellationToken)
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
                throw new InvalidOperationException($"Training data does not contain required column: {nameof(SentimentData.Text)}");
            }
            if (schema.GetColumnOrNull(nameof(SentimentData.Sentiment)) == null)
            {
                throw new InvalidOperationException($"Training data does not contain required column: {nameof(SentimentData.Sentiment)}");
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

            _logger.LogInformation("Starting AutoML experiment for sentiment classification with timeout of {Timeout} seconds...", experimentSettings.MaxExperimentTimeInSeconds);
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
            string modelDirectory = Path.GetDirectoryName(modelPath) ?? throw new InvalidOperationException("Invalid model path");
            Directory.CreateDirectory(modelDirectory);
            _mlContext.Model.Save(combinedModel, trainingData.Schema, modelPath);
            _logger.LogInformation("Trained model saved to {ModelPath}", modelPath);

            // Create prediction engine for immediate use (using combined model)
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<SentimentData, SentimentPrediction>(combinedModel);
        }, cancellationToken);
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
        [ColumnName("PredictedLabel")]
        public string? PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float[]? Score { get; set; }
    }
}

