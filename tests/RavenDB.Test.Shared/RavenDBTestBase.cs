using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Session;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RavenDB.AspNetCore.IdentityCore;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenDB.Test.Shared
{
    /// <summary>
    /// Base class for RavenDB tests using Docker container.
    ///
    /// IMPORTANT: Start the Docker container before running tests:
    ///   docker-compose up -d
    ///
    /// RavenDB Studio URL: http://127.0.0.1:8080
    /// Access the management studio in your browser to inspect databases during testing.
    ///
    /// Stop and cleanup:
    ///   docker-compose down -v
    /// </summary>
    public abstract class RavenDBTestBase : IDisposable
    {
        protected const string ServerUrl = "http://127.0.0.1:8080";
        private readonly List<(IDocumentStore store, string databaseName)> _storesToCleanup = new();

        /// <summary>
        /// Gets a new document store for testing with a unique database name.
        /// The database is automatically created and cleaned up.
        /// </summary>
        /// <param name="caller">Automatically populated with caller method name.</param>
        /// <returns>A new IDocumentStore instance.</returns>
        public IDocumentStore GetDocumentStore([CallerMemberName] string? caller = null)
        {
            var databaseName = $"TestDb_{caller}_{Guid.NewGuid():N}";

            var store = new DocumentStore
            {
                Urls = new[] { ServerUrl },
                Database = databaseName
            };

            store.Initialize();

            // Create the database (uses Docker container storage)
            var databaseRecord = new Raven.Client.ServerWide.DatabaseRecord(databaseName);

            store.Maintenance.Server.Send(new Raven.Client.ServerWide.Operations.CreateDatabaseOperation(databaseRecord));

            // Track for cleanup
            _storesToCleanup.Add((store, databaseName));

            return store;
        }

        /// <summary>
        /// Waits for all indexes to finish indexing and for compare exchange operations to complete.
        /// This is important for tests that query right after inserting data.
        /// RavenDB uses eventual consistency, so we need to wait for operations to propagate.
        /// </summary>
        /// <param name="store">The document store.</param>
        /// <param name="timeout">Timeout in seconds. Default is 15 seconds.</param>
        protected async Task WaitForIndexing(IDocumentStore store, int timeout = 15)
        {
            var databaseCommands = store.Maintenance.ForDatabase(store.Database);
            var sp = System.Diagnostics.Stopwatch.StartNew();

            while (sp.Elapsed.TotalSeconds < timeout)
            {
                var stats = await databaseCommands.SendAsync(new Raven.Client.Documents.Operations.GetStatisticsOperation());

                if (stats.StaleIndexes.Length == 0)
                {
                    // Give a little extra time for compare exchange operations to settle
                    await Task.Delay(100);
                    return;
                }

                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Waits for non-stale results when querying.
        /// Use this in queries that need immediate consistency.
        /// </summary>
        protected async Task WaitForNonStaleResults(IDocumentStore store, TimeSpan? timeout = null)
        {
            await WaitForIndexing(store, (int)(timeout?.TotalSeconds ?? 15));
        }

        /// <summary>
        /// Disposes resources used by the test.
        /// Cleans up all test databases created during the test.
        /// </summary>
        public virtual void Dispose()
        {
            // Clean up all databases created during this test
            foreach (var (store, databaseName) in _storesToCleanup)
            {
                try
                {
                    // Delete the database
                    store.Maintenance.Server.Send(
                        new Raven.Client.ServerWide.Operations.DeleteDatabasesOperation(
                            databaseName,
                            hardDelete: true));
                }
                catch
                {
                    // Ignore cleanup errors - database might already be gone
                }
                finally
                {
                    try
                    {
                        store.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }

            _storesToCleanup.Clear();
        }

        /// <summary>
        /// Cleanup method for tests.
        /// Note: Stop the Docker container with 'docker-compose down -v' to clean up all test data.
        /// </summary>
        public static void ShutdownServer()
        {
            // Docker container runs independently - use docker-compose to stop
            Console.WriteLine("Tests complete. Stop the Docker container with 'docker-compose down -v' if needed.");
        }

        /// <summary>
        /// Opens a browser to the RavenDB Studio for the given database.
        /// Useful for debugging tests and inspecting data.
        /// </summary>
        /// <param name="store">The document store.</param>
        protected void OpenStudioInBrowser(IDocumentStore store)
        {
            var url = $"http://127.0.0.1:8080/studio/index.html#databases/documents?database={store.Database}";
            Console.WriteLine($"RavenDB Studio URL for {store.Database}:");
            Console.WriteLine(url);

            try
            {
                // Try to open browser (works on macOS, Linux, Windows)
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                // If opening browser fails, URL is already printed to console
            }
        }

        /// <summary>
        /// Creates options for RavenRoleStore with AutoSaveChanges enabled.
        /// Use this when creating role stores in tests to ensure changes are automatically saved.
        /// </summary>
        protected IOptions<RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>> GetRoleOptions<TRole>()
            where TRole : RavenIdentityRole
        {
            var options = new RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>
            {
                AutoSaveChanges = true
            };
            return Options.Create(options);
        }

        /// <summary>
        /// Creates options for RavenUserStore with AutoSaveChanges enabled.
        /// Use this when creating user stores in tests to ensure changes are automatically saved.
        /// </summary>
        protected IOptions<RavenIdentityUserOptions<TUser, IAsyncDocumentSession>> GetUserOptions<TUser>()
            where TUser : RavenIdentityUser
        {
            var options = new RavenIdentityUserOptions<TUser, IAsyncDocumentSession>
            {
                AutoSaveChanges = true
            };
            return Options.Create(options);
        }
    }
}
