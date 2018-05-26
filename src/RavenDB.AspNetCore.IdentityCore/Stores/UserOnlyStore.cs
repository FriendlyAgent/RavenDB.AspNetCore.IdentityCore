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
    public class UserOnlyStore
        : UserOnlyStore<IdentityUser>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserOnlyStore(
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
    public class UserOnlyStore<TUser>
        : UserOnlyStore<TUser, IAsyncDocumentSession>
        where TUser : IdentityUser
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserOnlyStore(
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
    public class UserOnlyStore<TUser, TSession>
        : UserOnlyStore<TUser, TSession, IdentityUserClaim, IdentityUserLogin, IdentityUserToken>
        where TUser : IdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor"></param>
        public UserOnlyStore(
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
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    public class UserOnlyStore<TUser, TSession, TUserClaim, TUserLogin, TUserToken>
        : UserStoreBase<TUser, TSession, TUserClaim, TUserLogin, TUserToken>
        where TUser : IdentityUser
        where TSession : IAsyncDocumentSession
        where TUserClaim : IdentityUserClaim, new()
        where TUserLogin : IdentityUserLogin, new()
        where TUserToken : IdentityUserToken, new()
    { 
        public UserOnlyStore(
            TSession session, 
            IdentityErrorDescriber describer = null, 
            IOptions<IdentityOptions> optionsAccessor = null) 
            : base(session, describer, optionsAccessor)
        {
        }
    }
}