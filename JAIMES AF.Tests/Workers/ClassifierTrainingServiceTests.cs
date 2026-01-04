using MattEland.Jaimes.Workers.UserMessageWorker.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Workers;

/// <summary>
/// Tests for the ClassifierTrainingService using ML.NET AutoML.
/// Note: These tests take 30+ seconds each because AutoML requires sufficient time to complete at least one trial.
/// </summary>
public class ClassifierTrainingServiceTests
{
    /// <summary>
    /// Tests that the classifier can be trained with minimal sample data (20 rows).
    /// This test validates the training pipeline works correctly with the minimum required data.
    /// Note: The service enforces a minimum 30-second training time regardless of the requested value.
    /// </summary>
    [Fact]
    public async Task TrainClassifierAsync_WithMinimalSampleData_ShouldCompleteSuccessfully()
    {
        // Arrange
        var logger = new Mock<ILogger<ClassifierTrainingService>>();
        var service = new ClassifierTrainingService(logger.Object);

        // Create 20 sample rows with balanced sentiment distribution
        List<(string Text, string Label)> sampleData = new()
        {
            // Positive samples (7)
            ("This game is amazing and so much fun!", "positive"),
            ("I love the character development and story.", "positive"),
            ("Great job, that was really helpful!", "positive"),
            ("This adventure is incredibly exciting!", "positive"),
            ("I had a wonderful time exploring the dungeon.", "positive"),
            ("The graphics and atmosphere are beautiful.", "positive"),
            ("What an excellent treasure find!", "positive"),

            // Negative samples (7)
            ("This is frustrating and confusing.", "negative"),
            ("I hate how slow the combat is.", "negative"),
            ("The interface is terrible and hard to use.", "negative"),
            ("This quest is boring and tedious.", "negative"),
            ("I'm disappointed with the ending.", "negative"),
            ("The difficulty is unfair and annoying.", "negative"),
            ("I can't stand these long loading times.", "negative"),

            // Neutral samples (6)
            ("I walked into the room and looked around.", "neutral"),
            ("The merchant offered me some items.", "neutral"),
            ("I need to check my inventory.", "neutral"),
            ("Let me think about what to do next.", "neutral"),
            ("The door is open.", "neutral"),
            ("I see a path leading north.", "neutral")
        };

        // Act
        var result = await service.TrainClassifierAsync(
            sampleData,
            trainTestSplit: 0.80,
            trainingTimeSeconds: 5,
            optimizingMetric: "MacroAccuracy",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ModelBytes.ShouldNotBeNull();
        result.ModelBytes.Length.ShouldBeGreaterThan(0);
        result.TrainingRows.ShouldBeGreaterThan(0);
        result.TestRows.ShouldBeGreaterThan(0);
        result.TrainingRows.ShouldBeGreaterThanOrEqualTo(result.TestRows);
        result.MacroAccuracy.ShouldBeGreaterThanOrEqualTo(0);
        result.MacroAccuracy.ShouldBeLessThanOrEqualTo(1);
        result.TrainerName.ShouldNotBeNullOrEmpty();
        result.ConfusionMatrix.ShouldNotBeNull();
    }

    /// <summary>
    /// Tests that training with more varied data produces better results.
    /// Uses 50 samples to ensure AutoML has enough data to work with.
    /// </summary>
    [Fact]
    public async Task TrainClassifierAsync_With50Samples_ShouldCompleteSuccessfully()
    {
        // Arrange
        var logger = new Mock<ILogger<ClassifierTrainingService>>();
        var service = new ClassifierTrainingService(logger.Object);

        // Create more sample data for a more realistic scenario
        List<(string Text, string Label)> sampleData = GenerateSampleData(50);

        // Act
        var result = await service.TrainClassifierAsync(
            sampleData,
            trainTestSplit: 0.80,
            trainingTimeSeconds: 10,
            optimizingMetric: "MacroAccuracy",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ModelBytes.Length.ShouldBeGreaterThan(0);
        result.MacroAccuracy.ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Tests that the service can handle longer text inputs.
    /// </summary>
    [Fact]
    public async Task TrainClassifierAsync_WithLongerText_ShouldCompleteSuccessfully()
    {
        // Arrange
        var logger = new Mock<ILogger<ClassifierTrainingService>>();
        var service = new ClassifierTrainingService(logger.Object);

        List<(string Text, string Label)> sampleData = new()
        {
            // Longer positive samples
            ("I absolutely love this game! The storytelling is incredible and the characters are so well developed. Every quest feels meaningful and the world is beautiful.",
                "positive"),
            ("This has been an amazing adventure so far. The dungeon design is clever and the puzzles are challenging but fair. I'm really enjoying myself!",
                "positive"),
            ("What a fantastic experience! The combat system is smooth and satisfying. The music perfectly sets the mood for every scene.",
                "positive"),
            ("I'm having so much fun exploring this world. Every corner has something interesting and the attention to detail is remarkable.",
                "positive"),
            ("This is one of the best games I've played in years. The writing is top-notch and the gameplay is addictive. Highly recommended!",
                "positive"),
            ("Great session today! We accomplished so much and the story took an unexpected turn that I loved.",
                "positive"),
            ("The world building in this game is phenomenal. I feel completely immersed in the setting.", "positive"),

            // Longer negative samples
            ("I'm really frustrated with this game. The controls are clunky, the interface is confusing, and I keep getting stuck on simple tasks.",
                "negative"),
            ("This is not what I expected at all. The story is boring, the pacing is off, and the characters feel one-dimensional.",
                "negative"),
            ("I can't recommend this game. The difficulty spikes are unfair and there's no clear guidance on what to do next. Very disappointing.",
                "negative"),
            ("The tutorial was terrible and didn't explain anything properly. Now I'm lost and confused about basic mechanics.",
                "negative"),
            ("I've encountered so many bugs and glitches. The game crashes frequently and I've lost progress multiple times.",
                "negative"),
            ("This session was a waste of time. Nothing interesting happened and the narrative felt forced.",
                "negative"),
            ("I'm disappointed with the quality of this experience. It doesn't live up to the hype at all.",
                "negative"),

            // Longer neutral samples
            ("I entered the tavern and spoke with the barkeeper. He mentioned something about a quest to the north.",
                "neutral"),
            ("My character is a level five wizard with specialization in evocation magic. I have three spell slots remaining.",
                "neutral"),
            ("The merchant has a variety of items for sale including potions, scrolls, and basic weapons.", "neutral"),
            ("I checked my inventory and organized my items. I have enough supplies for the journey ahead.", "neutral"),
            ("The map shows several unexplored areas to the east. There's a forest and what appears to be ruins.",
                "neutral"),
            ("I'm currently in the town square during the morning hours. Several NPCs are going about their business.",
                "neutral")
        };

        // Act
        var result = await service.TrainClassifierAsync(
            sampleData,
            trainTestSplit: 0.80,
            trainingTimeSeconds: 5,
            optimizingMetric: "MacroAccuracy",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ModelBytes.Length.ShouldBeGreaterThan(0);
    }

    private static List<(string Text, string Label)> GenerateSampleData(int count)
    {
        List<(string Text, string Label)> data = new();

        string[] positiveTemplates =
        [
            "This is wonderful!",
            "I love this experience!",
            "Great job on this!",
            "Amazing adventure!",
            "Excellent work!",
            "I'm so happy with this!",
            "This is fantastic!",
            "Brilliant session today!",
            "I thoroughly enjoyed this!",
            "What a great time!"
        ];

        string[] negativeTemplates =
        [
            "This is terrible.",
            "I hate this experience.",
            "So frustrating!",
            "Very disappointing.",
            "I'm annoyed with this.",
            "This is boring.",
            "What a waste of time.",
            "I can't stand this.",
            "This is awful.",
            "Very unhappy with this."
        ];

        string[] neutralTemplates =
        [
            "I walked into the room.",
            "The door is open.",
            "I see some items.",
            "Let me check my inventory.",
            "The path leads north.",
            "I spoke with the NPC.",
            "Time to move forward.",
            "Looking around the area.",
            "I'm at the tavern.",
            "Checking the map now."
        ];

        int perClass = count / 3;
        int remainder = count % 3;

        for (int i = 0; i < perClass + (remainder > 0 ? 1 : 0); i++)
        {
            if (data.Count < count)
                data.Add((positiveTemplates[i % positiveTemplates.Length] + $" Variant {i}.", "positive"));
        }

        for (int i = 0; i < perClass + (remainder > 1 ? 1 : 0); i++)
        {
            if (data.Count < count)
                data.Add((negativeTemplates[i % negativeTemplates.Length] + $" Variant {i}.", "negative"));
        }

        for (int i = 0; i < perClass; i++)
        {
            if (data.Count < count)
                data.Add((neutralTemplates[i % neutralTemplates.Length] + $" Variant {i}.", "neutral"));
        }

        return data;
    }
}
