using System.Collections.Generic;
using MongoDB.Driver;
using Moq;

namespace MattEland.Jaimes.Tests.TestUtilities;

public static class MongoCollectionMockExtensions
{
    public static void SetupFindSequence<T>(
        this Mock<IMongoCollection<T>> collectionMock,
        params T?[] results) where T : class
    {
        Queue<T?> sequence = new(results);

        IFindFluent<T, T> CreateFindFluent()
        {
            Mock<IFindFluent<T, T>> findFluentMock = new();
            T? next = sequence.Count > 0 ? sequence.Dequeue() : null;
            findFluentMock.Setup(f => f.FirstOrDefaultAsync(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken _) => Task.FromResult(next));
            return findFluentMock.Object;
        }

        collectionMock
            .Setup(c => c.Find(
                It.IsAny<FilterDefinition<T>>(),
                It.IsAny<FindOptions>()))
            .Returns(() => CreateFindFluent());

        collectionMock
            .Setup(c => c.Find(
                It.IsAny<FilterDefinition<T>>(),
                (FindOptions?)null))
            .Returns(() => CreateFindFluent());
    }
}
