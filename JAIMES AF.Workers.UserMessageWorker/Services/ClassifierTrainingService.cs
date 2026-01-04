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

            int trainingRows = (int)splitData.TrainSet.GetRowCount()!;
            int testRows = (int)splitData.TestSet.GetRowCount()!;

            logger.LogInformation("Split data: {TrainingRows} training, {TestRows} test", trainingRows, testRows);

            // Configure AutoML experiment
            MulticlassClassificationMetric metric = ParseMetric(optimizingMetric);
            MulticlassExperimentSettings experimentSettings = new()
            {
                MaxExperimentTimeInSeconds = (uint)trainingTimeSeconds,
                OptimizingMetric = metric
            };

            // Featurize text
            logger.LogInformation("Featurizing text data...");
            IEstimator<ITransformer> textFeaturizer = _mlContext.Transforms.Text
                .FeaturizeText("Features", nameof(SentimentTrainingData.Text));

            ITransformer textTransformer = textFeaturizer.Fit(splitData.TrainSet);
            IDataView featurizedTrainData = textTransformer.Transform(splitData.TrainSet);
            IDataView featurizedTestData = textTransformer.Transform(splitData.TestSet);

            ColumnInformation columnInfo = new()
            {
                LabelColumnName = nameof(SentimentTrainingData.Sentiment)
            };

            logger.LogInformation("Starting AutoML experiment for {Time} seconds...", trainingTimeSeconds);
            ExperimentResult<MulticlassClassificationMetrics> experimentResult = _mlContext.Auto()
                .CreateMulticlassClassificationExperiment(experimentSettings)
                .Execute(featurizedTrainData, columnInfo);

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
        }, cancellationToken);
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
                    matrix[i][j] = (int)cm.Counts[i][j];
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
