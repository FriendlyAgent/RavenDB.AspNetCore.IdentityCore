using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Represents all the user options you can use to configure the Raven Identity system.
    /// </summary>
    public class RavenIdentityUserOptions
        : RavenIdentityUserOptions<RavenIdentityUser>
    {
    }

    /// <summary>
    /// Represents all the user options you can use to configure the Raven Identity system.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class RavenIdentityUserOptions<TUser>
        : RavenIdentityUserOptions<TUser, IAsyncDocumentSession>
        where TUser : RavenIdentityUser
    {
    }

    /// <summary>
    /// Represents all the user options you can use to configure the ravem identity system.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenIdentityUserOptions<TUser, TSession>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Specifies options for query the database.
        /// </summary>
        public class QueryOptions
        {
            /// <summary>
            /// The query used for getting the user by name.
            /// </summary>
            public Func<TSession, string, CancellationToken, Task<TUser>> GetUserByNameAsync { get; set; }

            /// <summary>
            /// The query used for getting the user by email.
            /// </summary>
            public Func<TSession, string, CancellationToken, Task<TUser>> GetUserByEmailAsync { get; set; }

            /// <summary>
            /// The query used for getting the user by login.
            /// </summary>
            public Func<TSession, string, string, CancellationToken, Task<TUser>> GetUserByLoginAsync { get; set; }

            /// <summary>
            /// The query used for getting the users in a role.
            /// </summary>
            public Func<TSession, string, CancellationToken, Task<List<TUser>>> GetUsersInRoleAsync { get; set; }

            /// <summary>
            /// The query used for getting the user for claim.
            /// </summary>
            public Func<TSession, Claim, CancellationToken, Task<List<TUser>>> GetUsersForClaimAsync { get; set; }
        }

        /// <summary>
        /// Gets or sets the Query for the identity system.
        /// </summary>
        public QueryOptions Query { get; set; }
            = new QueryOptions()
            {
                GetUserByNameAsync = delegate (
                    TSession session,
                    string normalizedUserName,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.NormalizedUserName == normalizedUserName)
                        .FirstOrDefaultAsync(cancellationToken);
                },
                GetUserByEmailAsync = delegate (
                    TSession session,
                    string normalizedEmail,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Email.NormalizedEmail == normalizedEmail)
                        .FirstOrDefaultAsync(cancellationToken);
                },
                GetUsersForClaimAsync = delegate (
                    TSession session,
                    Claim claim,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Claims.Any(
                            b => b.ClaimType == claim.Type && b.ClaimValue == claim.Value))
                        .ToListAsync(cancellationToken);
                },
                GetUserByLoginAsync = delegate (
                    TSession session,
                    string loginProvider,
                    string providerKey,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .SingleOrDefaultAsync(
                            a => a.Logins.Any(
                                b => b.LoginProvider == loginProvider && b.ProviderKey == loginProvider), cancellationToken);
                },
                GetUsersInRoleAsync = delegate (
                    TSession session,
                    string roleId,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Roles.Contains(roleId))
                        .ToListAsync(cancellationToken);
                }
            };
    }
}
