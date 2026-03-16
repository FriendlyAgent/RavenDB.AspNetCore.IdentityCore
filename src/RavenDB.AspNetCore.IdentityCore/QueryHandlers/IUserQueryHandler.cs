using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.QueryHandlers
{
    /// <summary>
    /// Provides an abstraction for querying users in the identity system.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public interface IUserQueryHandler<TUser, TSession>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Finds a user by their normalized username.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedUserName">The normalized username to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        Task<TUser> GetByNameAsync(TSession session, string normalizedUserName, CancellationToken cancellationToken);

        /// <summary>
        /// Finds a user by their normalized email address.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedEmail">The normalized email to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        Task<TUser> GetByEmailAsync(TSession session, string normalizedEmail, CancellationToken cancellationToken);

        /// <summary>
        /// Finds a user by their external login provider.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The provider key.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        Task<TUser> GetByLoginAsync(TSession session, string loginProvider, string providerKey, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all users in a specific role.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="roleId">The role ID.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A list of users in the role.</returns>
        Task<List<TUser>> GetUsersInRoleAsync(TSession session, string roleId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all users that have a specific claim.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="claim">The claim to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A list of users with the claim.</returns>
        Task<List<TUser>> GetUsersForClaimAsync(TSession session, Claim claim, CancellationToken cancellationToken);
    }
}
