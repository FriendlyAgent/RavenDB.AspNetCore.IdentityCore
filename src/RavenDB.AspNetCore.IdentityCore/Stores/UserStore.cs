using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Stores;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Represents a new instance of a persistence store for users, using the default implementation
    /// of <see cref="IdentityUser"/> with a string as a primary key.
    /// </summary>
    public class UserStore
        : UserStore<IdentityUser>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null)
            : base(session, describer, optionsAccessor)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class UserStore<TUser>
        : UserStore<TUser, IdentityRole, IAsyncDocumentSession>
        where TUser : IdentityUser
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null)
            : base(session, describer, optionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class UserStore<TUser, TRole>
        : UserStore<TUser, TRole, IAsyncDocumentSession, IdentityUserClaim, IdentityUserLogin, IdentityUserToken, IdentityRoleClaim>
        where TUser : IdentityUser
        where TRole : IdentityRole
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null)
            : base(session, describer, optionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class UserStore<TUser, TRole, TSession>
        : UserStore<TUser, TRole, TSession, IdentityUserClaim, IdentityUserLogin, IdentityUserToken, IdentityRoleClaim>
        where TUser : IdentityUser
        where TRole : IdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null)
            : base(session, describer, optionsAccessor)
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
    public class UserStore<TUser, TRole, TSession, TUserClaim, TUserLogin, TUserToken, TRoleClaim>
        : UserStoreBase<TUser, TRole, TSession, TUserClaim, TUserLogin, TUserToken, TRoleClaim>
        where TUser : IdentityUser
        where TRole : IdentityRole
        where TSession : IAsyncDocumentSession
        where TUserClaim : IdentityUserClaim, new()
        where TUserLogin : IdentityUserLogin, new()
        where TUserToken : IdentityUserToken, new()
        where TRoleClaim : IdentityRoleClaim, new()
    {
        public UserStore(
            TSession session, 
            IdentityErrorDescriber describer = null, 
            IOptions<IdentityOptions> optionsAccessor = null) 
            : base(session, describer, optionsAccessor)
        {
        }
    }
}