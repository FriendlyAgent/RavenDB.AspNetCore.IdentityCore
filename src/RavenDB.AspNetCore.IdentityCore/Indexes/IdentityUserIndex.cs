using Raven.Client.Documents.Indexes;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System.Collections.Generic;
using System.Linq;

namespace RavenDB.AspNetCore.IdentityCore.Indexes
{
    /// <summary>
    /// Static index for <see cref="RavenIdentityUser"/> that indexes commonly queried fields.
    /// </summary>
    /// <remarks>
    /// This index must be deployed to the RavenDB server before using static index queries.
    /// It indexes: NormalizedUserName, Email.NormalizedEmail, Logins, Roles, and Claims.
    /// </remarks>
    public class IdentityUserIndex : IdentityUserIndex<RavenIdentityUser>
    {
    }

    /// <summary>
    /// Static index for users that indexes commonly queried fields.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class IdentityUserIndex<TUser> : AbstractIndexCreationTask<TUser, IdentityUserIndex<TUser>.Result>
        where TUser : RavenIdentityUser
    {
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public override string IndexName => "IdentityUserIndex";

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserIndex{TUser}"/>.
        /// </summary>
        public IdentityUserIndex()
        {
            Map = users => from user in users
                           select new Result
                           {
                               NormalizedUserName = user.NormalizedUserName,
                               Email_NormalizedEmail = user.Email.NormalizedEmail,
                               Logins = user.Logins.Select(l => new Result.LoginEntry
                               {
                                   LoginProvider = l.LoginProvider,
                                   ProviderKey = l.ProviderKey
                               }),
                               Roles = user.Roles,
                               Claims = user.Claims.Select(c => new Result.ClaimEntry
                               {
                                   ClaimType = c.ClaimType,
                                   ClaimValue = c.ClaimValue
                               })
                           };

            Store(x => x.NormalizedUserName, FieldStorage.Yes);
        }

        /// <summary>
        /// Result model for <see cref="IdentityUserIndex{TUser}"/> queries.
        /// </summary>
        public class Result
        {
            /// <summary>
            /// Gets or sets the normalized user name.
            /// </summary>
            public string NormalizedUserName { get; set; }

            /// <summary>
            /// Gets or sets the normalized email address.
            /// </summary>
            public string Email_NormalizedEmail { get; set; }

            /// <summary>
            /// Gets or sets the external logins for this user.
            /// </summary>
            public IEnumerable<LoginEntry> Logins { get; set; }

            /// <summary>
            /// Gets or sets the role IDs this user belongs to.
            /// </summary>
            public IEnumerable<string> Roles { get; set; }

            /// <summary>
            /// Gets or sets the claims for this user.
            /// </summary>
            public IEnumerable<ClaimEntry> Claims { get; set; }

            /// <summary>
            /// Represents a login entry in the index result.
            /// </summary>
            public class LoginEntry
            {
                /// <summary>
                /// Gets or sets the login provider name.
                /// </summary>
                public string LoginProvider { get; set; }

                /// <summary>
                /// Gets or sets the provider key.
                /// </summary>
                public string ProviderKey { get; set; }
            }

            /// <summary>
            /// Represents a claim entry in the index result.
            /// </summary>
            public class ClaimEntry
            {
                /// <summary>
                /// Gets or sets the claim type.
                /// </summary>
                public string ClaimType { get; set; }

                /// <summary>
                /// Gets or sets the claim value.
                /// </summary>
                public string ClaimValue { get; set; }
            }
        }
    }
}
