using System.Diagnostics;
using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using MattEland.Jaimes.Workers.UserMessageWorker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MattEland.Jaimes.Workers.UserMessageWorker.Consumers;

/// <summary>
/// Consumer for processing classifier training requests.
/// </summary>
public class ClassifierTrainingConsumer(
    IDbContextFactory<JaimesDbContext> contextFactory,
    ILogger<ClassifierTrainingConsumer> logger,
    ActivitySource activitySource,
    ClassifierTrainingService trainingService,
    IClassificationModelService classificationModelService,
    IMessageUpdateNotifier messageUpdateNotifier) : IMessageConsumer<TrainClassifierMessage>
{
    public async Task HandleAsync(TrainClassifierMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = activitySource.StartActivity("ClassifierTraining.Process");
        activity?.SetTag("training.job_id", message.TrainingJobId);
        activity?.SetTag("training.min_confidence", message.MinConfidence);
        activity?.SetTag("training.train_test_split", message.TrainTestSplit);
        activity?.SetTag("training.time_seconds", message.TrainingTimeSeconds);
        activity?.SetTag("training.metric", message.OptimizingMetric);

        try
        {
            logger.LogInformation(
                "Starting classifier training job #{JobId} (Confidence: {Confidence:P0}, Split: {Split:P0}, Time: {Time}s, Metric: {Metric})",
                message.TrainingJobId,
                message.MinConfidence,
                message.TrainTestSplit,
                message.TrainingTimeSeconds,
                message.OptimizingMetric);

            // Update job status to Training
            await classificationModelService.UpdateTrainingJobAsync(
                message.TrainingJobId,
                "Training",
                cancellationToken: cancellationToken);

            // Notify clients that training has started
            await NotifyStatusChanged(message.TrainingJobId, "Training", cancellationToken);

            // Get training data from database
            await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            List<(string Text, string Label)> trainingData = await context.MessageSentiments
                .Include(ms => ms.Message)
                .Where(ms =>
                    ms.Message != null &&
                    !string.IsNullOrWhiteSpace(ms.Message.Text) &&
                    (ms.SentimentSource == SentimentSource.Player ||
                     (ms.Confidence != null && ms.Confidence >= message.MinConfidence)))
                .Select(ms => new {ms.Message!.Text, ms.Sentiment})
                .ToListAsync(cancellationToken)
                .ContinueWith(t => t.Result.Select(x => (x.Text, MapSentimentToLabel(x.Sentiment))).ToList(),
                    cancellationToken);

            if (trainingData.Count < 20)
            {
                string errorMessage =
                    $"Insufficient training data: only {trainingData.Count} rows found (minimum 20 required)";
                logger.LogError("Training job #{JobId} failed: {Error}", message.TrainingJobId, errorMessage);

                await classificationModelService.UpdateTrainingJobAsync(
                    message.TrainingJobId,
                    "Failed",
                    totalRows: trainingData.Count,
                    errorMessage: errorMessage,
                    cancellationToken: cancellationToken);

                await NotifyTrainingCompleted(message.TrainingJobId, null, false, errorMessage, cancellationToken);
                return;
            }

            activity?.SetTag("training.total_rows", trainingData.Count);

            // Train the model
            ClassifierTrainingResult result = await trainingService.TrainClassifierAsync(
                trainingData,
                message.TrainTestSplit,
                message.TrainingTimeSeconds,
                message.OptimizingMetric,
                cancellationToken);

            activity?.SetTag("training.trainer_name", result.TrainerName);
            activity?.SetTag("training.macro_accuracy", result.MacroAccuracy);

            // Upload the model
            string modelName = $"Custom Classifier {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
            ClassificationModelResponse uploadedModel = await classificationModelService.UploadModelAsync(
                ClassificationModelTypes.SentimentClassification,
                modelName,
                "CustomSentimentModel.zip",
                result.ModelBytes,
                $"Trained from {trainingData.Count} messages with {message.MinConfidence:P0} confidence threshold",
                message.TrainingJobId,
                cancellationToken);

            // Update training job with results
            await classificationModelService.UpdateTrainingJobAsync(
                message.TrainingJobId,
                "Completed",
                totalRows: trainingData.Count,
                trainingRows: result.TrainingRows,
                testRows: result.TestRows,
                macroAccuracy: result.MacroAccuracy,
                microAccuracy: result.MicroAccuracy,
                macroPrecision: result.MacroPrecision,
                macroRecall: result.MacroRecall,
                logLoss: result.LogLoss,
                confusionMatrix: result.ConfusionMatrix,
                trainerName: result.TrainerName,
                classificationModelId: uploadedModel.Id,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Training job #{JobId} completed successfully. Model ID: {ModelId}, Accuracy: {Accuracy:P2}",
                message.TrainingJobId,
                uploadedModel.Id,
                result.MacroAccuracy);

            await NotifyTrainingCompleted(message.TrainingJobId, uploadedModel.Id, true, null, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Training job #{JobId} failed with error", message.TrainingJobId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            await classificationModelService.UpdateTrainingJobAsync(
                message.TrainingJobId,
                "Failed",
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);

            await NotifyTrainingCompleted(message.TrainingJobId, null, false, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task NotifyTrainingCompleted(int jobId,
        int? modelId,
        bool success,
        string? error,
        CancellationToken ct)
    {
        try
        {
            ClassifierTrainingCompletedNotification notification = new(jobId, modelId, success, error);
            await messageUpdateNotifier.NotifyClassifierTrainingCompletedAsync(notification, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send training completion notification for job #{JobId}", jobId);
        }
    }

    private async Task NotifyStatusChanged(int jobId, string status, CancellationToken ct)
    {
        try
        {
            await messageUpdateNotifier.NotifyClassifierTrainingStatusChangedAsync(jobId, status, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send training status notification for job #{JobId}", jobId);
        }
    }

    private static string MapSentimentToLabel(int sentiment) => sentiment switch
    {
        1 => "positive",
        -1 => "negative",
        _ => "neutral"
    };
}
