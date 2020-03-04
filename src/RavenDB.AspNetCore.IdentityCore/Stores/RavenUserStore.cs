using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Stores;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Represents a new instance of a persistence store for users, using the default implementation of <see cref="RavenIdentityUser"/>.
    /// </summary>
    public class RavenUserStore
        : RavenUserStore<RavenIdentityUser>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<RavenIdentityUser, IAsyncDocumentSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<RavenIdentityRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class RavenUserStore<TUser>
        : RavenUserStore<TUser, RavenIdentityRole, IAsyncDocumentSession>
        where TUser : RavenIdentityUser
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, IAsyncDocumentSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<RavenIdentityRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class RavenUserStore<TUser, TRole>
        : RavenUserStore<TUser, TRole, IAsyncDocumentSession, RavenIdentityUserClaim, RavenIdentityUserLogin, RavenIdentityUserToken, RavenIdentityRoleClaim>
        where TUser : RavenIdentityUser
        where TRole : RavenIdentityRole
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, IAsyncDocumentSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenUserStore<TUser, TRole, TSession>
        : RavenUserStore<TUser, TRole, TSession, RavenIdentityUserClaim, RavenIdentityUserLogin, RavenIdentityUserToken, RavenIdentityRoleClaim>
        where TUser : RavenIdentityUser
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TRoleClaim">The type representing a user role.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    public class RavenUserStore<TUser, TRole, TSession, TUserClaim, TUserLogin, TUserToken, TRoleClaim>
        : RavenUserStoreBase<TUser, TRole, TSession, TUserClaim, TUserLogin, TUserToken, TRoleClaim>,
        IProtectedUserStore<TUser>
        where TUser : RavenIdentityUser
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
        where TUserClaim : RavenIdentityUserClaim, new()
        where TUserLogin : RavenIdentityUserLogin, new()
        where TUserToken : RavenIdentityUserToken, new()
        where TRoleClaim : RavenIdentityRoleClaim, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStore(
            TSession session, 
            IdentityErrorDescriber describer = null, 
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor, ravenRoleOptionsAccessor)
        {
        }
    }
}