using Raven.Client.Documents.Indexes;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System.Linq;

namespace RavenDB.AspNetCore.IdentityCore.Indexes
{
    /// <summary>
    /// Static index for <see cref="RavenIdentityRole"/> that indexes commonly queried fields.
    /// </summary>
    /// <remarks>
    /// This index must be deployed to the RavenDB server before using static index queries.
    /// It indexes: NormalizedRoleName.
    /// </remarks>
    public class IdentityRoleIndex : IdentityRoleIndex<RavenIdentityRole>
    {
    }

    /// <summary>
    /// Static index for roles that indexes commonly queried fields.
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class IdentityRoleIndex<TRole> : AbstractIndexCreationTask<TRole, IdentityRoleIndex<TRole>.Result>
        where TRole : RavenIdentityRole
    {
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public override string IndexName => "IdentityRoleIndex";

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityRoleIndex{TRole}"/>.
        /// </summary>
        public IdentityRoleIndex()
        {
            Map = roles => from role in roles
                           select new Result
                           {
                               NormalizedRoleName = role.NormalizedRoleName
                           };

            Store(x => x.NormalizedRoleName, FieldStorage.Yes);
        }

        /// <summary>
        /// Result model for <see cref="IdentityRoleIndex{TRole}"/> queries.
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Gets or sets the normalized role name.
            /// </summary>
            public string NormalizedRoleName { get; set; }
        }
    }
}
