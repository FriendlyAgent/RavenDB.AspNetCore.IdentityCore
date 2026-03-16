using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.QueryHandlers
{
    /// <summary>
    /// Default implementation of <see cref="IUserQueryHandler{TUser, TSession}"/> for querying users
    /// using a custom static index.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TIndex">The static index type to use for queries. Must derive from <see cref="AbstractCommonApiForIndexes"/>.</typeparam>
    /// <remarks>
    /// Use this overload when you have a custom user type (e.g. ApplicationUser) and a custom index
    /// that targets the correct collection. The custom index should have the same fields as
    /// <see cref="IdentityUserIndex{TUser}"/>.
    /// </remarks>
    public class DefaultUserQueryHandler<TUser, TSession, TIndex> : IUserQueryHandler<TUser, TSession>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
        where TIndex : AbstractCommonApiForIndexes, new()
    {
        private readonly RavenIdentityUserOptions<TUser, TSession> _options;

        /// <summary>
        /// Initializes a new instance of <see cref="DefaultUserQueryHandler{TUser, TSession, TIndex}"/>.
        /// </summary>
        /// <param name="options">The options for configuring the query handler.</param>
        public DefaultUserQueryHandler(IOptions<RavenIdentityUserOptions<TUser, TSession>> options = null)
        {
            _options = options?.Value ?? new RavenIdentityUserOptions<TUser, TSession>();
        }

        /// <summary>
        /// Creates a query against the configured static index.
        /// </summary>
        private static IRavenQueryable<TUser> QueryIndex(TSession session)
        {
            return session.Query<TUser, TIndex>();
        }

        /// <summary>
        /// Finds a user by their normalized username.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedUserName">The normalized username to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        public virtual Task<TUser> GetByNameAsync(TSession session, string normalizedUserName, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return QueryIndex(session)
                    .Where(a => a.NormalizedUserName == normalizedUserName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return session
                .Query<TUser>()
                .Where(a => a.NormalizedUserName == normalizedUserName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Finds a user by their normalized email address.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedEmail">The normalized email to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        public virtual Task<TUser> GetByEmailAsync(TSession session, string normalizedEmail, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return QueryIndex(session)
                    .Where(a => a.Email.NormalizedEmail == normalizedEmail)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return session
                .Query<TUser>()
                .Where(a => a.Email.NormalizedEmail == normalizedEmail)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Finds a user by their external login provider.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The provider key.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if found, otherwise null.</returns>
        public virtual async Task<TUser> GetByLoginAsync(TSession session, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return await QueryIndex(session)
                    .SingleOrDefaultAsync(
                        a => a.Logins.Any(
                            b => b.LoginProvider == loginProvider && b.ProviderKey == providerKey), cancellationToken);
            }

            // Query by provider KEY only (more unique) to avoid multi-field Any() issue
            // Provider keys are unique per user, so this returns fewer results
            var usersWithProviderKey = await session
                .Query<TUser>()
                .Where(a => a.Logins.Any(b => b.ProviderKey == providerKey))
                .ToListAsync(cancellationToken);

            // Filter in memory for the specific login provider
            return usersWithProviderKey
                .SingleOrDefault(u => u.Logins.Any(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey));
        }

        /// <summary>
        /// Gets all users in a specific role.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="roleId">The role ID.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A list of users in the role.</returns>
        public virtual Task<List<TUser>> GetUsersInRoleAsync(TSession session, string roleId, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return QueryIndex(session)
                    .Where(a => a.Roles.Contains(roleId))
                    .ToListAsync(cancellationToken);
            }

            return session
                .Query<TUser>()
                .Where(a => a.Roles.Contains(roleId))
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets all users that have a specific claim.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="claim">The claim to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A list of users with the claim.</returns>
        public virtual async Task<List<TUser>> GetUsersForClaimAsync(TSession session, Claim claim, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return await QueryIndex(session)
                    .Where(a => a.Claims.Any(
                        b => b.ClaimType == claim.Type && b.ClaimValue == claim.Value))
                    .ToListAsync(cancellationToken);
            }

            // Query by claim VALUE only (more unique) to avoid multi-field Any() issue
            // This returns fewer users than querying by type
            var usersWithClaimValue = await session
                .Query<TUser>()
                .Where(a => a.Claims.Any(b => b.ClaimValue == claim.Value))
                .ToListAsync(cancellationToken);

            // Filter in memory for the specific claim type
            return [.. usersWithClaimValue.Where(u => u.Claims.Any(c => c.ClaimType == claim.Type && c.ClaimValue == claim.Value))];
        }
    }

    /// <summary>
    /// Default implementation of <see cref="IUserQueryHandler{TUser, TSession}"/> for querying users
    /// using the default <see cref="IdentityUserIndex{TUser}"/>.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class DefaultUserQueryHandler<TUser, TSession> : DefaultUserQueryHandler<TUser, TSession, IdentityUserIndex<TUser>>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DefaultUserQueryHandler{TUser, TSession}"/>.
        /// </summary>
        /// <param name="options">The options for configuring the query handler.</param>
        public DefaultUserQueryHandler(IOptions<RavenIdentityUserOptions<TUser, TSession>> options = null)
            : base(options)
        {
        }
    }
}
