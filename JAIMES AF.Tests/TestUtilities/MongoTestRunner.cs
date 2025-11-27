using Mongo2Go;
using MongoDB.Driver;

namespace MattEland.Jaimes.Tests.TestUtilities;

public sealed class MongoTestRunner : IDisposable
{
    private readonly MongoDbRunner runner;

    public MongoTestRunner()
    {
        runner = MongoDbRunner.Start(singleNodeReplSet: true);
        Client = new MongoClient(runner.ConnectionString);
    }

    public IMongoClient Client { get; }

    public void ResetDatabase()
    {
        const string databaseName = "documents";

        try
        {
            Client.DropDatabase(databaseName);
        }
        catch (MongoCommandException ex) when (ex.Code == 26 || ex.CodeName == "NamespaceNotFound")
        {
            // Database has not been created yet; safe to ignore.
        }
    }

    public void Dispose()
    {
        runner.Dispose();
    }
}
