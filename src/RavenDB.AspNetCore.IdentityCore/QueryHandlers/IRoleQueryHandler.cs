using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.QueryHandlers
{
    /// <summary>
    /// Provides an abstraction for querying roles in the identity system.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public interface IRoleQueryHandler<TRole, TSession>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Finds a role by its normalized name.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedRoleName">The normalized role name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The role if found, otherwise null.</returns>
        Task<TRole> GetByNameAsync(TSession session, string normalizedRoleName, CancellationToken cancellationToken);
    }
}
