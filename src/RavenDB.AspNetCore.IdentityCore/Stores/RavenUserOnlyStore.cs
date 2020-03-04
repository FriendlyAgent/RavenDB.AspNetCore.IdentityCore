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
    public class RavenUserOnlyStore
        : RavenUserOnlyStore<RavenIdentityUser>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserOnlyStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        public RavenUserOnlyStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<RavenIdentityUser, IAsyncDocumentSession>> ravenUserOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class RavenUserOnlyStore<TUser>
        : RavenUserOnlyStore<TUser, IAsyncDocumentSession>
        where TUser : RavenIdentityUser
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserOnlyStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        public RavenUserOnlyStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, IAsyncDocumentSession>> ravenUserOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenUserOnlyStore<TUser, TSession>
        : RavenUserOnlyStore<TUser, TSession, RavenIdentityUserClaim, RavenIdentityUserLogin, RavenIdentityUserToken>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserOnlyStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        public RavenUserOnlyStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor)
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
    public class RavenUserOnlyStore<TUser, TSession, TUserClaim, TUserLogin, TUserToken>
        : RavenUserOnlyStoreBase<TUser, TSession, TUserClaim, TUserLogin, TUserToken>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
        where TUserClaim : RavenIdentityUserClaim, new()
        where TUserLogin : RavenIdentityUserLogin, new()
        where TUserToken : RavenIdentityUserToken, new()
    {

        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserOnlyStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor"></param>
        public RavenUserOnlyStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor)
        {

        }
    }
}