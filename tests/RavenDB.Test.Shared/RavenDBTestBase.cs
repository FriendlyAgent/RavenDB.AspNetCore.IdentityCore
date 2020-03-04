using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using System;
using System.Threading;

namespace RavenDB.Test.Shared
{
    public abstract class RavenDBTestBase
    {
        public DocumentStore GetDocumentStore()
        {
            var name = "EasyFlor-UnitTest-" + Guid.NewGuid().ToString().Replace("-", string.Empty).Substring(0, 8);
            var store = new DocumentStore()
            {
                Database = name,
                Urls = new[] { "http://localhost:8080" }
            };

            store.Initialize();

            var doc = new DatabaseRecord(name);

            store.Maintenance.Server.Send(new CreateDatabaseOperation(doc));
            store.AfterDispose += (sender, args) =>
            {
                Thread.Sleep(500);
                //store.Maintenance.Server.Send(new DeleteDatabasesOperation(name, true));
            };

            return store;
        }
    }
}
