using Mongo2Go;
using MongoDB.Driver;

namespace MattEland.Jaimes.Tests.TestUtilities;

public sealed class MongoTestRunner : IDisposable
{
    private readonly MongoDbRunner runner;

    static MongoTestRunner()
    {
        EnsureLegacyOpenSslLibraries();
    }

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

    private static void EnsureLegacyOpenSslLibraries()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string libDirectory = Path.Combine(AppContext.BaseDirectory, "mongo-libs");
        if (!Directory.Exists(libDirectory))
        {
            return;
        }

        string? ldLibraryPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        if (!string.IsNullOrEmpty(ldLibraryPath))
        {
            string[] segments = ldLibraryPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Contains(libDirectory, StringComparer.Ordinal))
            {
                return;
            }
        }

        string newValue = string.IsNullOrEmpty(ldLibraryPath)
            ? libDirectory
            : string.Concat(libDirectory, ":", ldLibraryPath);

        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newValue);
    }
}
