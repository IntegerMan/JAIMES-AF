using MattEland.Jaimes.ServiceDefinitions.Messages;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints;

/// <summary>
/// Endpoint for starting classifier training.
/// </summary>
public class StartClassifierTrainingEndpoint : Endpoint<TrainClassifierRequest, StartTrainingResponse>
{
    public required IClassificationModelService ClassificationModelService { get; set; }
    public required IMessagePublisher MessagePublisher { get; set; }

    public override void Configure()
    {
        Post("/admin/classification-models/train");
        AllowAnonymous();
        Description(b => b
            .Produces<StartTrainingResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Admin"));
    }

    public override async Task HandleAsync(TrainClassifierRequest req, CancellationToken ct)
    {
        // Validate parameters
        if (req.MinConfidence is < 0.0 or > 1.0)
        {
            AddError("MinConfidence must be between 0.0 and 1.0");
        }

        if (req.TrainTestSplit is < 0.5 or > 0.95)
        {
            AddError("TrainTestSplit must be between 0.5 and 0.95");
        }

        int[] validTimes = [5, 30, 60, 120, 300, 600];
        if (!validTimes.Contains(req.TrainingTimeSeconds))
        {
            AddError("TrainingTimeSeconds must be one of: 5, 30, 60, 120, 300, 600");
        }

        string[] validMetrics = ["MacroAccuracy", "MicroAccuracy", "LogLoss", "TopKAccuracy"];
        if (!validMetrics.Contains(req.OptimizingMetric))
        {
            AddError($"OptimizingMetric must be one of: {string.Join(", ", validMetrics)}");
        }

        ThrowIfAnyErrors();

        // Check we have enough training data
        TrainingDataCountResponse countResponse = await ClassificationModelService.GetTrainingDataCountAsync(req.MinConfidence, ct);
        if (countResponse.MessagesAboveConfidence < 20)
        {
            AddError($"Insufficient training data. Need at least 20 messages above {req.MinConfidence:P0} confidence, but only found {countResponse.MessagesAboveConfidence}");
            ThrowIfAnyErrors();
        }

        // Create the training job
        int jobId = await ClassificationModelService.CreateTrainingJobAsync(
            req.MinConfidence,
            req.TrainTestSplit,
            req.TrainingTimeSeconds,
            req.OptimizingMetric,
            ct);

        // Enqueue message for worker
        TrainClassifierMessage message = new()
        {
            TrainingJobId = jobId,
            MinConfidence = req.MinConfidence,
            TrainTestSplit = req.TrainTestSplit,
            TrainingTimeSeconds = req.TrainingTimeSeconds,
            OptimizingMetric = req.OptimizingMetric
        };

        await MessagePublisher.PublishAsync(message, ct);

        await Send.CreatedAtAsync<GetClassificationModelDetailsEndpoint>(
            new { id = jobId },
            new StartTrainingResponse(jobId, "Queued"),
            cancellation: ct);
    }
}
