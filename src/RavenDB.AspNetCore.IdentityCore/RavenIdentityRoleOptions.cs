using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Represents all the role options you can use to configure the ravem identity system.
    /// </summary>
    public class RavenIdentityRoleOptions
        : RavenIdentityRoleOptions<RavenIdentityRole>
    {
    }

    /// <summary>
    /// Represents all the role options you can use to configure the ravem identity system.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class RavenIdentityRoleOptions<TRole>
        : RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>
        where TRole : RavenIdentityRole
    {
    }

    /// <summary>
    /// Represents all the role options you can use to configure the ravem identity system.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenIdentityRoleOptions<TRole, TSession>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Specifies options for query the database.
        /// </summary>
        public class QueryOptions
        {
            /// <summary>
            /// The query used for getting the role by name.
            /// </summary>
            public Func<TSession, string, CancellationToken, Task<TRole>> GetRoleByNameAsync { get; set; }
        }

        /// <summary>
        /// Gets or sets the Query for the identity system.
        /// </summary>
        public QueryOptions Query { get; set; }
            = new QueryOptions()
            {
                GetRoleByNameAsync = delegate (
                    TSession session,
                    string normalizedRoleName,
                    CancellationToken cancellationToken)
                    {
                        return session
                            .Query<TRole>()
                            .Where(a => a.NormalizedRoleName == normalizedRoleName)
                            .FirstOrDefaultAsync(cancellationToken);
                    },
            };
    }
}
