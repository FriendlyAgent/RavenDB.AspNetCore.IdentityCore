using IdentitySample.ApiEndpoints.Indexes;
using Raven.Client.Documents;

namespace IdentitySample.ApiEndpoints.Data;

public static class DatabaseInitializer
{
    public static void Initialize(IDocumentStore store)
    {
        EnsureServerIsReachable(store);

        if (!DatabaseExists(store))
        {
            CreateDatabase(store);
            DeployIndexes(store);
        }
    }

    private static void EnsureServerIsReachable(IDocumentStore store)
    {
        try
        {
            store.Maintenance.Server.Send(new Raven.Client.ServerWide.Operations.GetBuildNumberOperation());
        }
        catch (Exception ex) when (ex is HttpRequestException or AggregateException)
        {
            var urls = string.Join(", ", store.Urls);
            throw new InvalidOperationException(
                $"Cannot connect to RavenDB at [{urls}]. " +
                $"Make sure the Docker container is running: docker compose up -d",
                ex);
        }
    }

    private static bool DatabaseExists(IDocumentStore store)
    {
        var dbNames = store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.GetDatabaseNamesOperation(0, int.MaxValue));

        return dbNames.Contains(store.Database);
    }

    private static void CreateDatabase(IDocumentStore store)
    {
        var dbRecord = new Raven.Client.ServerWide.DatabaseRecord(store.Database);
        store.Maintenance.Server.Send(
            new Raven.Client.ServerWide.Operations.CreateDatabaseOperation(dbRecord));
    }

    private static void DeployIndexes(IDocumentStore store)
    {
        new ApplicationUserIndex().Execute(store);
    }
}
