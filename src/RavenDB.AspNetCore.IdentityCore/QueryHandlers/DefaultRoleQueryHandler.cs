using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.QueryHandlers
{
    /// <summary>
    /// Default implementation of <see cref="IRoleQueryHandler{TRole, TSession}"/> for querying roles
    /// using a custom static index.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TIndex">The static index type to use for queries. Must derive from <see cref="AbstractCommonApiForIndexes"/>.</typeparam>
    /// <remarks>
    /// Use this overload when you have a custom role type and a custom index
    /// that targets the correct collection. The custom index should have the same fields as
    /// <see cref="IdentityRoleIndex{TRole}"/>.
    /// </remarks>
    public class DefaultRoleQueryHandler<TRole, TSession, TIndex> : IRoleQueryHandler<TRole, TSession>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
        where TIndex : AbstractCommonApiForIndexes, new()
    {
        private readonly RavenIdentityRoleOptions<TRole, TSession> _options;

        /// <summary>
        /// Initializes a new instance of <see cref="DefaultRoleQueryHandler{TRole, TSession, TIndex}"/>.
        /// </summary>
        /// <param name="options">The options for configuring the query handler.</param>
        public DefaultRoleQueryHandler(IOptions<RavenIdentityRoleOptions<TRole, TSession>> options = null)
        {
            _options = options?.Value ?? new RavenIdentityRoleOptions<TRole, TSession>();
        }

        /// <summary>
        /// Creates a query against the configured static index.
        /// </summary>
        private static IRavenQueryable<TRole> QueryIndex(TSession session)
        {
            return session.Query<TRole, TIndex>();
        }

        /// <summary>
        /// Finds a role by its normalized name.
        /// </summary>
        /// <param name="session">The database session.</param>
        /// <param name="normalizedRoleName">The normalized role name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The role if found, otherwise null.</returns>
        public virtual Task<TRole> GetByNameAsync(TSession session, string normalizedRoleName, CancellationToken cancellationToken)
        {
            if (_options.UseStaticIndexes)
            {
                return QueryIndex(session)
                    .Where(a => a.NormalizedRoleName == normalizedRoleName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return session
                .Query<TRole>()
                .Where(a => a.NormalizedRoleName == normalizedRoleName)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Default implementation of <see cref="IRoleQueryHandler{TRole, TSession}"/> for querying roles
    /// using the default <see cref="IdentityRoleIndex{TRole}"/>.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class DefaultRoleQueryHandler<TRole, TSession> : DefaultRoleQueryHandler<TRole, TSession, IdentityRoleIndex<TRole>>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Initializes a new instance of <see cref="DefaultRoleQueryHandler{TRole, TSession}"/>.
        /// </summary>
        /// <param name="options">The options for configuring the query handler.</param>
        public DefaultRoleQueryHandler(IOptions<RavenIdentityRoleOptions<TRole, TSession>> options = null)
            : base(options)
        {
        }
    }
}
