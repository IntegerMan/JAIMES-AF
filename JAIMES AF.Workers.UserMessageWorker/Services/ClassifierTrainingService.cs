using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Services;

/// <summary>
/// Result of classifier training.
/// </summary>
public record ClassifierTrainingResult(
    byte[] ModelBytes,
    int TrainingRows,
    int TestRows,
    double MacroAccuracy,
    double MicroAccuracy,
    double MacroPrecision,
    double MacroRecall,
    double LogLoss,
    int[][] ConfusionMatrix,
    string TrainerName);

/// <summary>
/// Service for training classification models using ML.NET AutoML.
/// </summary>
public class ClassifierTrainingService(ILogger<ClassifierTrainingService> logger)
{
    private readonly MLContext _mlContext = new(seed: 0);

    /// <summary>
    /// Trains a classifier from the provided training data.
    /// </summary>
    public async Task<ClassifierTrainingResult> TrainClassifierAsync(
        List<(string Text, string Label)> data,
        double trainTestSplit,
        int trainingTimeSeconds,
        string optimizingMetric,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
            {
                logger.LogInformation(
                    "Starting classifier training with {Count} rows, {Split:P0} split, {Time}s time, {Metric} metric",
                    data.Count,
                    trainTestSplit,
                    trainingTimeSeconds,
                    optimizingMetric);

                // Convert to IDataView
                IEnumerable<SentimentTrainingData> trainingData = data.Select(d => new SentimentTrainingData
                {
                    Text = d.Text,
                    Sentiment = d.Label
                });

                IDataView dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                // Split into train/test
                DataOperationsCatalog.TrainTestData splitData = _mlContext.Data.TrainTestSplit(
                    dataView,
                    testFraction: 1.0 - trainTestSplit,
                    seed: 0);

                // GetRowCount() can return null for some data views, so handle gracefully
                long? trainRowCount = splitData.TrainSet.GetRowCount();
                long? testRowCount = splitData.TestSet.GetRowCount();
                int trainingRows = trainRowCount.HasValue
                    ? (int) trainRowCount.Value
                    : data.Count - (int) (data.Count * (1.0 - trainTestSplit));
                int testRows = testRowCount.HasValue ? (int) testRowCount.Value : data.Count - trainingRows;

                logger.LogInformation("Split data: {TrainingRows} training, {TestRows} test", trainingRows, testRows);

                // Configure AutoML experiment with a minimum training time to ensure at least one trial completes
                // The 5-second option is for quick testing but may fail with very small datasets
                int effectiveTrainingTime = Math.Max(trainingTimeSeconds, 30); // Ensure at least 30 seconds
                if (effectiveTrainingTime != trainingTimeSeconds)
                {
                    logger.LogWarning(
                        "Training time increased from {Requested}s to {Effective}s to ensure successful completion",
                        trainingTimeSeconds,
                        effectiveTrainingTime);
                }

                MulticlassClassificationMetric metric = ParseMetric(optimizingMetric);
                MulticlassExperimentSettings experimentSettings = new()
                {
                    MaxExperimentTimeInSeconds = (uint) effectiveTrainingTime,
                    OptimizingMetric = metric,
                    CacheBeforeTrainer = CacheBeforeTrainer.On // Cache data for faster training
                };

                // Featurize text
                logger.LogInformation("Featurizing text data...");
                IEstimator<ITransformer> textFeaturizer = _mlContext.Transforms.Text
                    .FeaturizeText("Features", nameof(SentimentTrainingData.Text));

                ITransformer textTransformer = textFeaturizer.Fit(splitData.TrainSet);
                IDataView featurizedTrainData = textTransformer.Transform(splitData.TrainSet);

                ColumnInformation columnInfo = new()
                {
                    LabelColumnName = nameof(SentimentTrainingData.Sentiment)
                };

                logger.LogInformation("Starting AutoML experiment for {Time} seconds...", effectiveTrainingTime);
                ExperimentResult<MulticlassClassificationMetrics> experimentResult;
                try
                {
                    experimentResult = _mlContext.Auto()
                        .CreateMulticlassClassificationExperiment(experimentSettings)
                        .Execute(featurizedTrainData, columnInfo);
                }
                catch (TimeoutException ex)
                {
                    throw new InvalidOperationException(
                        $"AutoML training timed out after {effectiveTrainingTime} seconds without completing a successful trial. " +
                        "This can happen with very small datasets or insufficient training time. " +
                        "Try increasing training time or adding more training data.",
                        ex);
                }

                logger.LogInformation(
                    "AutoML completed. Best trainer: {TrainerName}, MacroAccuracy: {Accuracy:P2}",
                    experimentResult.BestRun.TrainerName,
                    experimentResult.BestRun.ValidationMetrics?.MacroAccuracy ?? 0);

                // Combine text featurizer with best model
                ITransformer combinedModel = textTransformer.Append(experimentResult.BestRun.Model);

                // Evaluate on test set
                IDataView predictions = combinedModel.Transform(splitData.TestSet);
                MulticlassClassificationMetrics metrics = _mlContext.MulticlassClassification.Evaluate(
                    predictions,
                    labelColumnName: nameof(SentimentTrainingData.Sentiment));

                // Get confusion matrix
                int[][] confusionMatrix = ExtractConfusionMatrix(metrics);

                // Save model to bytes
                using MemoryStream modelStream = new();
                _mlContext.Model.Save(combinedModel, dataView.Schema, modelStream);
                byte[] modelBytes = modelStream.ToArray();

                logger.LogInformation("Model saved ({Size} bytes)", modelBytes.Length);

                return new ClassifierTrainingResult(
                    modelBytes,
                    trainingRows,
                    testRows,
                    metrics.MacroAccuracy,
                    metrics.MicroAccuracy,
                    0, // MacroPrecision - not directly available in metrics object
                    0, // MacroRecall - not directly available in metrics object
                    metrics.LogLoss,
                    confusionMatrix,
                    experimentResult.BestRun.TrainerName);
            },
            cancellationToken);
    }

    private static MulticlassClassificationMetric ParseMetric(string metric) => metric switch
    {
        "MacroAccuracy" => MulticlassClassificationMetric.MacroAccuracy,
        "MicroAccuracy" => MulticlassClassificationMetric.MicroAccuracy,
        "LogLoss" => MulticlassClassificationMetric.LogLoss,
        "TopKAccuracy" => MulticlassClassificationMetric.TopKAccuracy,
        _ => MulticlassClassificationMetric.MacroAccuracy
    };

    private static int[][] ExtractConfusionMatrix(MulticlassClassificationMetrics metrics)
    {
        // Extract counts from confusion matrix
        // For 3-class (negative=-1, neutral=0, positive=1), we'll create a 3x3 matrix
        try
        {
            ConfusionMatrix? cm = metrics.ConfusionMatrix;
            if (cm == null)
            {
                return [[0, 0, 0], [0, 0, 0], [0, 0, 0]];
            }

            int numClasses = cm.NumberOfClasses;
            int[][] matrix = new int[numClasses][];
            for (int i = 0; i < numClasses; i++)
            {
                matrix[i] = new int[numClasses];
                for (int j = 0; j < numClasses; j++)
                {
                    matrix[i][j] = (int) cm.Counts[i][j];
                }
            }

            return matrix;
        }
        catch
        {
            return [[0, 0, 0], [0, 0, 0], [0, 0, 0]];
        }
    }

    /// <summary>
    /// Input data structure for sentiment training.
    /// </summary>
    public class SentimentTrainingData
    {
        public string Text { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
    }
}
