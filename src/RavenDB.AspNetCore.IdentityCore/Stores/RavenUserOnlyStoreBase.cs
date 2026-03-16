using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Helpers;
using RavenDB.AspNetCore.IdentityCore.QueryHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Stores
{
    /// <summary>
    /// Creates a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    public abstract class RavenUserOnlyStoreBase<TUser, TSession, TUserClaim, TUserLogin, TUserToken> :
           IUserLoginStore<TUser>,
           IUserClaimStore<TUser>,
           IUserPasswordStore<TUser>,
           IUserSecurityStampStore<TUser>,
           IUserEmailStore<TUser>,
           IUserLockoutStore<TUser>,
           IUserPhoneNumberStore<TUser>,
           IUserTwoFactorStore<TUser>,
           IUserAuthenticationTokenStore<TUser>,
           IUserAuthenticatorKeyStore<TUser>,
           IUserTwoFactorRecoveryCodeStore<TUser>,
           IQueryableUserStore<TUser>
           where TUser : RavenIdentityUser
           where TSession : IAsyncDocumentSession
           where TUserClaim : RavenIdentityUserClaim, new()
           where TUserLogin : RavenIdentityUserLogin, new()
           where TUserToken : RavenIdentityUserToken, new()
    {
        /// <summary>
        /// Gets or sets a flag indicating if changes should be persisted after CreateAsync, UpdateAsync and DeleteAsync are called.
        /// </summary>
        /// <value>
        /// True if changes should be automatically persisted, otherwise false.
        /// </value>
        public bool AutoSaveChanges { get; set; } = true;

        /// <summary>
        /// Gets or sets the <see cref="IdentityErrorDescriber"/> for any error that occurred with the current operation.
        /// </summary>
        public IdentityErrorDescriber ErrorDescriber { get; set; }

        /// <summary>
        /// The <see cref="IdentityOptions"/> used to configure Identity.
        /// </summary>
        public IdentityOptions Options { get; set; }

        /// <summary>
        /// The <see cref="RavenIdentityUserOptions"/> used to configure Raven Identity.
        /// </summary>
        public RavenIdentityUserOptions<TUser, TSession> RavenUserOptions { get; set; }

        /// <summary>
        /// The query handler used for querying users.
        /// </summary>
        protected IUserQueryHandler<TUser, TSession> UserQueryHandler { get; set; }

        /// <summary>
        /// The helper for managing username reservations.
        /// </summary>
        private UserNameReservationHelper UserNameReservation { get; set; }

        /// <summary>
        /// The helper for managing email reservations.
        /// </summary>
        private EmailReservationHelper EmailReservation { get; set; }

        protected TSession _session { get; set; }

        private bool _disposed;

        /// <summary>
        /// Gets an IQueryable of users. NOT SUPPORTED - throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is required by the <see cref="IQueryableUserStore{TUser}"/> interface from
        /// ASP.NET Core Identity, which is outside of our control. However, RavenDB uses an async
        /// document session model that is fundamentally incompatible with <see cref="IQueryable{T}"/>.
        /// Exposing a queryable here would bypass RavenDB's session lifetime management and could lead
        /// to unexpected behavior with deferred query execution.
        /// </para>
        /// <para>
        /// Instead, inject <see cref="IAsyncDocumentSession"/> and query users directly:
        /// </para>
        /// <code>
        /// // Instead of: userManager.Users.Where(u => ...)
        /// // Use: session.Query&lt;TUser&gt;().Where(u => ...)
        /// </code>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown when accessed.</exception>
        public IQueryable<TUser> Users =>
            throw new NotSupportedException(
                "IQueryable access via UserManager.Users is not supported. " +
                "RavenDB's async document session model is incompatible with IQueryable. " +
                "This property exists because the IQueryableUserStore<TUser> interface requires it. " +
                "Inject IAsyncDocumentSession and use session.Query<TUser>() instead.");

        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="userQueryHandler">The query handler for user queries. If not provided, uses the default implementation.</param>
        /// <param name="loggerFactory">Optional logger factory for structured logging of Compare Exchange operations.</param>
        public RavenUserOnlyStoreBase(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null,
            IUserQueryHandler<TUser, TSession> userQueryHandler = null,
            ILoggerFactory loggerFactory = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            ErrorDescriber = describer ?? new IdentityErrorDescriber();

            Options = optionsAccessor?.Value ?? new IdentityOptions();

            RavenUserOptions = ravenUserOptionsAccessor?.Value ?? new RavenIdentityUserOptions<TUser, TSession>();

            UserQueryHandler = userQueryHandler ?? new DefaultUserQueryHandler<TUser, TSession>(Microsoft.Extensions.Options.Options.Create(RavenUserOptions));

            UserNameReservation = new UserNameReservationHelper(
                session.Advanced.DocumentStore,
                logger: loggerFactory?.CreateLogger<UserNameReservationHelper>(),
                releaseRetryCount: RavenUserOptions.ReservationReleaseRetryCount);
            EmailReservation = new EmailReservationHelper(
                session.Advanced.DocumentStore,
                logger: loggerFactory?.CreateLogger<EmailReservationHelper>(),
                releaseRetryCount: RavenUserOptions.ReservationReleaseRetryCount);

            _session = session;

            AutoSaveChanges = RavenUserOptions.AutoSaveChanges;
        }

        /// <summary>
        /// Called to create a new instance of a <see cref="RavenIdentityUserClaim"/>.
        /// </summary>
        /// <param name="user">The associated user.</param>
        /// <param name="claim">The associated claim.</param>
        /// <returns></returns>
        protected virtual TUserClaim CreateUserClaim(TUser user, Claim claim)
        {
            if (user.Claims.Any(x => x.Equals(claim)))
                throw new InvalidOperationException("Claim already exists.");

            return new TUserClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                CreatedOn = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Called to create a new instance of a <see cref="RavenIdentityUserLogin"/>.
        /// </summary>
        /// <param name="user">The associated user.</param>
        /// <param name="login">The associated login.</param>
        /// <returns></returns>
        protected virtual TUserLogin CreateUserLogin(TUser user, UserLoginInfo login)
        {
            if (user.Logins.Any(x => x.Equals(login)))
                throw new InvalidOperationException("Login already exists.");

            return new TUserLogin
            {
                ProviderKey = login.ProviderKey,
                LoginProvider = login.LoginProvider,
                ProviderDisplayName = login.ProviderDisplayName,
                CreatedOn = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Called to create a new instance of a <see cref="RavenIdentityUserToken"/>.
        /// </summary>
        /// <param name="user">The associated user.</param>
        /// <param name="loginProvider">The associated login provider.</param>
        /// <param name="name">The name of the user token.</param>
        /// <param name="value">The value of the user token.</param>
        /// <returns></returns>
        protected virtual TUserToken CreateUserToken(TUser user, string loginProvider, string name, string value)
        {
            return new TUserToken
            {
                LoginProvider = loginProvider,
                Name = name,
                Value = value,
                CreatedOn = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(claims);

            foreach (var claim in claims)
                user.Claims.Add(CreateUserClaim(user, claim));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(login);

            user.Logins.Add(CreateUserLogin(user, login));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the creation operation.</returns>
        public async virtual Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var normalizedUserName = user.NormalizedUserName ?? user.UserName;
            var normalizedEmail = user.Email?.NormalizedEmail ?? user.Email?.Email;

            if (RavenUserOptions.EnforceUniqueConstraints)
            {
                var usernameReserved = await UserNameReservation.TryReserveAsync(normalizedUserName, "pending", cancellationToken);

                if (!usernameReserved)
                {
                    return IdentityResult.Failed(ErrorDescriber.DuplicateUserName(user.UserName));
                }

                var emailReserved = false;
                if (Options.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    emailReserved = await EmailReservation.TryReserveAsync(normalizedEmail, "pending", cancellationToken);

                    if (!emailReserved)
                    {
                        await UserNameReservation.TryReleaseAsync(normalizedUserName, cancellationToken);
                        return IdentityResult.Failed(ErrorDescriber.DuplicateEmail(user.Email.Email));
                    }
                }

                try
                {
                    await _session.StoreAsync(user, cancellationToken).ConfigureAwait(false);

                    var usernameUpdated = await UserNameReservation.TryUpdateAsync(normalizedUserName, user.Id, cancellationToken);
                    if (!usernameUpdated)
                    {
                        _session.Advanced.Evict(user);
                        await UserNameReservation.TryReleaseAsync(normalizedUserName, cancellationToken);
                        if (Options.User.RequireUniqueEmail && emailReserved)
                        {
                            await EmailReservation.TryReleaseAsync(normalizedEmail, cancellationToken);
                        }
                        return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
                    }

                    if (Options.User.RequireUniqueEmail && emailReserved)
                    {
                        var emailUpdated = await EmailReservation.TryUpdateAsync(normalizedEmail, user.Id, cancellationToken);
                        if (!emailUpdated)
                        {
                            _session.Advanced.Evict(user);
                            await UserNameReservation.TryReleaseAsync(normalizedUserName, cancellationToken);
                            await EmailReservation.TryReleaseAsync(normalizedEmail, cancellationToken);
                            return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
                        }
                    }

                    await SaveChanges(cancellationToken: cancellationToken);
                }
                catch (Exception)
                {
                    _session.Advanced.Evict(user);
                    await UserNameReservation.TryReleaseAsync(normalizedUserName, cancellationToken);
                    if (Options.User.RequireUniqueEmail && emailReserved)
                    {
                        await EmailReservation.TryReleaseAsync(normalizedEmail, cancellationToken);
                    }
                    throw;
                }
            }
            else
            {
                await _session.StoreAsync(user, cancellationToken).ConfigureAwait(false);
                await SaveChanges(cancellationToken: cancellationToken);
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public async virtual Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var normalizedUserName = user.NormalizedUserName ?? user.UserName;
            var normalizedEmail = user.Email?.NormalizedEmail ?? user.Email?.Email;

            try
            {
                _session.Delete(user);
                await SaveChanges(cancellationToken: cancellationToken);

                if (RavenUserOptions.EnforceUniqueConstraints)
                {
                    await UserNameReservation.TryReleaseAsync(normalizedUserName, cancellationToken);

                    if (Options.User.RequireUniqueEmail && !string.IsNullOrWhiteSpace(normalizedEmail))
                    {
                        await EmailReservation.TryReleaseAsync(normalizedEmail, cancellationToken);
                    }
                }
            }
            catch (ConcurrencyException)
            {
                return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if any associated with the specified normalized email address.
        /// </returns>
        public virtual async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(normalizedEmail);

            return await UserQueryHandler
                .GetByEmailAsync(_session, normalizedEmail, cancellationToken);
        }


        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userId"/> if it exists.
        /// </returns>
        public virtual Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(userId);

            return _session.LoadAsync<TUser>(userId, cancellationToken);
        }

        /// <summary>
        /// Retrieves the user associated with the specified login provider and login provider key..
        /// </summary>
        /// <param name="loginProvider">The login provider who provided the <paramref name="providerKey"/>.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing the user, if any which matched the specified login provider and key.
        /// </returns>
        public virtual async Task<TUser> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(loginProvider);

            ArgumentNullException.ThrowIfNull(providerKey);

            return await UserQueryHandler
                .GetByLoginAsync(_session, loginProvider, providerKey, cancellationToken);
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified normalized user name.
        /// </summary>
        /// <param name="normalizedUserName">The normalized user name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="normalizedUserName"/> if it exists.
        /// </returns>
        public virtual async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(normalizedUserName);

            return await UserQueryHandler
                .GetByNameAsync(_session, normalizedUserName, cancellationToken);
        }

        /// <summary>
        /// Retrieves the current failed access count for the specified <paramref name="user"/>..
        /// </summary>
        /// <param name="user">The user whose failed access count should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the failed access count.</returns>
        public virtual Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.AccessFailedCount);
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a user.</returns>
        public virtual Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var claims = user.Claims.
                Select(claim => claim
                    .ToClaim())
                .ToList();

            return Task.FromResult<IList<Claim>>(claims);
        }

        /// <summary>
        /// Gets the email address for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose email should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The task object containing the results of the asynchronous operation, the email address for the specified <paramref name="user"/>.</returns>
        public virtual Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.GetEmail());
        }

        /// <summary>
        /// Gets a flag indicating whether the email address for the specified <paramref name="user"/> has been verified, true if the email address is verified otherwise
        /// false.
        /// </summary>
        /// <param name="user">The user whose email confirmation status should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous operation, a flag indicating whether the email address for the specified <paramref name="user"/>
        /// has been confirmed or not.
        /// </returns>
        public virtual Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            return user.Email == null
                ? throw new InvalidOperationException("Unable get the confirmation of the email, because the user doesn't have an email.")
                : Task.FromResult(user.IsEmailConfirmed());
        }

        /// <summary>
        /// Retrieves a flag indicating whether user lockout can enabled for the specified user.
        /// </summary>
        /// <param name="user">The user whose ability to be locked out should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, true if a user can be locked out, otherwise false.
        /// </returns>
        public virtual Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.IsLockoutEnabled);
        }

        /// <summary>
        /// Gets the last <see cref="DateTimeOffset"/> a user's last lockout expired, if any.
        /// Any time in the past should be indicates a user is not locked out.
        /// </summary>
        /// <param name="user">The user whose lockout date should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that represents the result of the asynchronous query, a <see cref="DateTimeOffset"/> containing the last time
        /// a user's lockout expired, if any.
        /// </returns>
        public virtual Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            DateTimeOffset? lockoutEndDate;
            if (user.LockoutEndDate != null)
                lockoutEndDate = new DateTimeOffset(user.LockoutEndDate.Value);
            else
                lockoutEndDate = default;

            return Task.FromResult(lockoutEndDate);
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public virtual Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var logins = user.Logins.
                Select(login => login
                    .ToUserLoginInfo())
                .ToList();

            return Task.FromResult<IList<UserLoginInfo>>(logins);
        }

        /// <summary>
        /// Returns the normalized email for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose email address to retrieve.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the normalized email address if any associated with the specified user.
        /// </returns>
        public virtual Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            string normalizedEmail = (user.Email != null) ?
                user.Email.NormalizedEmail : null;

            return Task.FromResult(normalizedEmail);
        }


        /// <summary>
        /// Gets the normalized user name for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose normalized name should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        public virtual Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.NormalizedUserName);
        }

        /// <summary>
        /// Gets the password hash for a user.
        /// </summary>
        /// <param name="user">The user to retrieve the password hash for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the password hash for the user.</returns>
        public virtual Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.PasswordHash);
        }

        /// <summary>
        /// Gets the telephone number, if any, for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose telephone number should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the user's telephone number, if any.</returns>
        public virtual Task<string?> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.GetPhoneNumber());
        }

        /// <summary>
        /// Gets a flag indicating whether the specified <paramref name="user"/>'s telephone number has been confirmed.
        /// </summary>
        /// <param name="user">The user to return a flag for, indicating whether their telephone number is confirmed.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, returning true if the specified <paramref name="user"/> has a confirmed
        /// telephone number otherwise false.
        /// </returns>
        public virtual Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            if (user.PhoneNumber == null)
                throw new InvalidOperationException("Unable get the confirmation of the phone number, because the user doesn't have an phone number.");

            return Task.FromResult(user.IsPhoneNumberConfirmed());
        }

        /// <summary>
        /// Get the security stamp for the specified <paramref name="user" />.
        /// </summary>
        /// <param name="user">The user whose security stamp should be set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the security stamp for the specified <paramref name="user"/>.</returns>
        public virtual Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.SecurityStamp);
        }

        /// <summary>
        /// Returns the token value.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="loginProvider">The authentication provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task<string> GetTokenAsync(
            TUser user,
            string loginProvider,
            string name,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var token = user.Tokens
                .SingleOrDefault(a => a.Name == name && a.LoginProvider == loginProvider);

            return await Task.FromResult(token?.Value);
        }

        private const string InternalLoginProvider = "[AspNetUserStore]";
        private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
        private const string RecoveryCodeTokenName = "RecoveryCodes";

        /// <summary>
        /// Returns a flag indicating whether the specified <paramref name="user"/> has two factor authentication enabled or not,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose two factor authentication enabled status should be set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing a flag indicating whether the specified 
        /// <paramref name="user"/> has two factor authentication enabled or not.
        /// </returns>
        public virtual Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.IsTwoFactorEnabled);
        }

        /// <summary>
        /// Gets the user identifier for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose identifier should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the identifier for the specified <paramref name="user"/>.</returns>
        public virtual Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.Id);
        }

        /// <summary>
        /// Gets the user name for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose name should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the name for the specified <paramref name="user"/>.</returns>
        public virtual Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.UserName);
        }

        /// <summary>
        /// Retrieves all users with the specified claim.
        /// </summary>
        /// <param name="claim">The claim whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that contain the specified claim. 
        /// </returns>
        public virtual async Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(claim);

            return await UserQueryHandler
                .GetUsersForClaimAsync(_session, claim, cancellationToken);
        }

        /// <summary>
        /// Returns a flag indicating if the specified user has a password.
        /// </summary>
        /// <param name="user">The user to retrieve the password hash for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a flag indicating if the specified user has a password. If the 
        /// user has a password the returned value with be true, otherwise it will be false.</returns>
        public virtual Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            return Task.FromResult(user.PasswordHash != null);
        }

        /// <summary>
        /// Records that a failed access has occurred, incrementing the failed access count.
        /// </summary>
        /// <param name="user">The user whose cancellation count should be incremented.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the incremented failed access count.</returns>
        public virtual Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.AccessFailedCount += 1;
            return Task.FromResult(user.AccessFailedCount);
        }


        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(claims);

            foreach (var claim in claims)
            {
                var userClaim = user.Claims
                    .SingleOrDefault(a => a.Equals(claim));

                if (userClaim != null)
                    user.Claims.Remove(userClaim);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes the <paramref name="loginProvider"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the login from.</param>
        /// <param name="loginProvider">The login to remove from the user.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(loginProvider);

            ArgumentNullException.ThrowIfNull(providerKey);

            var login = user.Logins
                .SingleOrDefault(a => a.ProviderKey == providerKey && a.LoginProvider == loginProvider);

            if (login != null)
                user.Logins.Remove(login);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes a token for a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="loginProvider">The authentication provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task RemoveTokenAsync(TUser user, string loginProvider, string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(loginProvider);

            ArgumentNullException.ThrowIfNull(name);

            var token = user.Tokens
                .SingleOrDefault(a => a.Name == name && a.LoginProvider == loginProvider);

            if (token != null)
                user.Tokens.Remove(token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Replaces the <paramref name="claim"/> on the specified <paramref name="user"/>, with the <paramref name="newClaim"/>.
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim replace.</param>
        /// <param name="newClaim">The new claim replacing the <paramref name="claim"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(claim);

            ArgumentNullException.ThrowIfNull(newClaim);

            var matchedClaims = user.Claims
                .Where(a => a.Equals(claim))
                .ToList();

            foreach (var matchedClaim in matchedClaims)
            {
                matchedClaim.ClaimValue = newClaim.Value;
                matchedClaim.ClaimType = newClaim.Type;
                matchedClaim.UpdatedOn = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Resets a user's failed access count.
        /// </summary>
        /// <param name="user">The user whose failed access count should be reset.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        /// <remarks>This is typically called after the account is successfully accessed.</remarks>
        public virtual Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the <paramref name="email"/> address for a <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose email should be set.</param>
        /// <param name="email">The email to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.Email = email != null ? new RavenIdentityUserEmail(email) : null;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the flag indicating whether the specified <paramref name="user"/>'s email address has been confirmed or not.
        /// </summary>
        /// <param name="user">The user whose email confirmation status should be set.</param>
        /// <param name="confirmed">A flag indicating if the email address has been confirmed, true if the address is confirmed otherwise false.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            if (user.Email == null)
                throw new InvalidOperationException("Unable to set the confirmation status of the email, because the user doesn't have an email.");

            if (confirmed)
                user.Email.SetConfirmed();
            else
                user.Email.SetUnconfirmed();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Set the flag indicating if the specified <paramref name="user"/> can be locked out..
        /// </summary>
        /// <param name="user">The user whose ability to be locked out should be set.</param>
        /// <param name="enabled">A flag indicating if lock out can be enabled for the specified <paramref name="user"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.IsLockoutEnabled = enabled;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Locks out a user until the specified end date has passed. Setting a end date in the past immediately unlocks a user.
        /// </summary>
        /// <param name="user">The user whose lockout date should be set.</param>
        /// <param name="lockoutEnd">The <see cref="DateTimeOffset"/> after which the <paramref name="user"/>'s lockout should end.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.LockoutEndDate = lockoutEnd?.UtcDateTime;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the normalized email for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose email address to set.</param>
        /// <param name="normalizedEmail">The normalized email to set for the specified <paramref name="user"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            if (normalizedEmail != null && user.Email != null)
                user.Email.NormalizedEmail = normalizedEmail;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the given normalized name for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose name should be set.</param>
        /// <param name="normalizedName">The normalized name to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        public virtual Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            user.NormalizedUserName = normalizedName ?? throw new ArgumentNullException(nameof(normalizedName));
            return Task.CompletedTask;
        }


        /// <summary>
        /// Sets the password hash for a user.
        /// </summary>
        /// <param name="user">The user to set the password hash for.</param>
        /// <param name="passwordHash">The password hash to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the telephone number for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose telephone number should be set.</param>
        /// <param name="phoneNumber">The telephone number to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.PhoneNumber = phoneNumber != null ? new RavenIdentityUserPhoneNumber(phoneNumber) : null;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets a flag indicating if the specified <paramref name="user"/>'s phone number has been confirmed..
        /// </summary>
        /// <param name="user">The user whose telephone number confirmation status should be set.</param>
        /// <param name="confirmed">A flag indicating whether the user's telephone number has been confirmed.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            if (user.PhoneNumber == null)
                throw new InvalidOperationException("Unable to set the confirmation status of the phone number, because the user doesn't have an phone number.");

            if (confirmed)
                user.PhoneNumber.SetConfirmed();
            else
                user.PhoneNumber.SetUnconfirmed();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the provided security <paramref name="stamp"/> for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose security stamp should be set.</param>
        /// <param name="stamp">The security stamp to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.SecurityStamp = stamp ?? throw new ArgumentNullException(nameof(stamp));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the token value for a particular user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="loginProvider">The authentication provider for the token.</param>
        /// <param name="name">The name of the token.</param>
        /// <param name="value">The value of the token.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetTokenAsync(TUser user, string loginProvider, string name, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var token = user.Tokens
                .SingleOrDefault(a => a.Name == name && a.LoginProvider == loginProvider);

            if (token == null)
                user.Tokens.Add(CreateUserToken(user, loginProvider, name, value));
            else
            {
                token.Value = value;
                token.UpdatedOn = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets a flag indicating whether the specified <paramref name="user"/> has two factor authentication enabled or not,
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose two factor authentication enabled status should be set.</param>
        /// <param name="enabled">A flag indicating whether the specified <paramref name="user"/> has two factor authentication enabled.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.IsTwoFactorEnabled = enabled;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the given <paramref name="userName" /> for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose name should be set.</param>
        /// <param name="userName">The user name to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            user.UserName = userName;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public virtual async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            // Check what changed in this session
            var changes = _session.Advanced.WhatChanged();
            var hasUserChanged = changes.TryGetValue(user.Id, out var userChanges);

            if (!hasUserChanged || userChanges == null || userChanges.Length == 0)
            {
                // No changes to this document
                return IdentityResult.Success;
            }

            // Check for username change
            var usernameChange = userChanges.FirstOrDefault(c =>
                c.FieldName == nameof(user.NormalizedUserName) || c.FieldName == nameof(user.UserName));
            var emailChange = userChanges.FirstOrDefault(c =>
                c.FieldName == nameof(user.Email) ||
                c.FieldName == "Email.NormalizedEmail" ||
                c.FieldName == "Email.Email");
            var phoneNumberChange = userChanges.FirstOrDefault(c =>
                c.FieldName == nameof(user.PhoneNumber) ||
                c.FieldName == "PhoneNumber.Number");

            string oldNormalizedUserName = null;
            string currentNormalizedUserName = user.NormalizedUserName ?? user.UserName;
            bool usernameChanged = false;

            if (usernameChange != null)
            {
                oldNormalizedUserName = usernameChange.FieldOldValue?.ToString();
                usernameChanged = !string.IsNullOrEmpty(oldNormalizedUserName) &&
                                 !string.Equals(oldNormalizedUserName, currentNormalizedUserName, StringComparison.OrdinalIgnoreCase);
            }

            string oldNormalizedEmail = null;
            string currentNormalizedEmail = user.Email?.NormalizedEmail ?? user.Email?.Email;
            bool emailChanged = false;

            if (Options.User.RequireUniqueEmail && emailChange != null)
            {
                // Handle case where entire Email object is replaced (FieldOldValue is RavenIdentityUserEmail)
                if (emailChange.FieldOldValue is RavenIdentityUserEmail oldEmailObj)
                {
                    oldNormalizedEmail = oldEmailObj.NormalizedEmail ?? oldEmailObj.Email;
                }
                else
                {
                    // Handle case where nested property changed (FieldOldValue is string)
                    oldNormalizedEmail = emailChange.FieldOldValue?.ToString();
                }

                emailChanged = !string.IsNullOrEmpty(oldNormalizedEmail) &&
                              !string.Equals(oldNormalizedEmail, currentNormalizedEmail, StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                if (RavenUserOptions.EnforceUniqueConstraints)
                {
                    if (usernameChanged)
                    {
                        var usernameReserved = await UserNameReservation.TryReserveAsync(currentNormalizedUserName, user.Id, cancellationToken);
                        if (!usernameReserved)
                        {
                            return IdentityResult.Failed(ErrorDescriber.DuplicateUserName(user.UserName));
                        }
                    }

                    if (emailChanged && !string.IsNullOrWhiteSpace(currentNormalizedEmail))
                    {
                        var emailReserved = await EmailReservation.TryReserveAsync(currentNormalizedEmail, user.Id, cancellationToken);
                        if (!emailReserved)
                        {
                            if (usernameChanged)
                            {
                                await UserNameReservation.TryReleaseAsync(currentNormalizedUserName, cancellationToken);
                            }
                            return IdentityResult.Failed(ErrorDescriber.DuplicateEmail(user.Email.Email));
                        }
                    }
                }

                // Update timestamps for contact information changes
                if (emailChange != null && user.Email != null)
                {
                    // If old value was not null, this is an update to existing email
                    if (emailChange.FieldOldValue != null && !(emailChange.FieldName == nameof(user.Email) && emailChange.FieldOldValue is null))
                    {
                        user.Email.UpdatedOn = DateTime.UtcNow;
                    }
                }

                if (phoneNumberChange != null && user.PhoneNumber != null)
                {
                    // If old value was not null, this is an update to existing phone number
                    if (phoneNumberChange.FieldOldValue != null && !(phoneNumberChange.FieldName == nameof(user.PhoneNumber) && phoneNumberChange.FieldOldValue is null))
                    {
                        user.PhoneNumber.UpdatedOn = DateTime.UtcNow;
                    }
                }

                user.ConcurrencyStamp = Guid.NewGuid().ToString();
                await SaveChanges(cancellationToken: cancellationToken);

                if (RavenUserOptions.EnforceUniqueConstraints)
                {
                    if (usernameChanged)
                    {
                        await UserNameReservation.TryReleaseAsync(oldNormalizedUserName, cancellationToken);
                    }

                    if (emailChanged && !string.IsNullOrWhiteSpace(oldNormalizedEmail))
                    {
                        await EmailReservation.TryReleaseAsync(oldNormalizedEmail, cancellationToken);
                    }
                }
            }
            catch (Exception)
            {
                if (RavenUserOptions.EnforceUniqueConstraints)
                {
                    if (usernameChanged)
                    {
                        await UserNameReservation.TryReleaseAsync(currentNormalizedUserName, cancellationToken);
                    }

                    if (emailChanged && !string.IsNullOrWhiteSpace(currentNormalizedEmail))
                    {
                        await EmailReservation.TryReleaseAsync(currentNormalizedEmail, cancellationToken);
                    }
                }
                throw;
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// Sets the authenticator key for the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user whose authenticator key should be set.</param>
        /// <param name="key">The authenticator key to set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken = default)
            => SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);

        /// <summary>
        /// Get the authenticator key for the specified <paramref name="user" />.
        /// </summary>
        /// <param name="user">The user whose security stamp should be set.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the security stamp for the specified <paramref name="user"/>.</returns>
        public Task<string> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken = default)
            => GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);

        /// <summary>
        /// Updates the recovery codes for the user while invalidating any previous recovery codes.
        /// </summary>
        /// <param name="user">The user to store new recovery codes for.</param>
        /// <param name="recoveryCodes">The new recovery codes for the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The new recovery codes for the user.</returns>
        public Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken = default)
        {
            var mergedCodes = string.Join(";", recoveryCodes);
            return SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
        }

        /// <summary>
        /// Returns whether a recovery code is valid for a user. Note: recovery codes are only valid
        /// once, and will be invalid after use.
        /// </summary>
        /// <param name="user">The user who owns the recovery code.</param>
        /// <param name="code">The recovery code to use.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>True if the recovery code was found for the user.</returns>
        public async Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            ArgumentNullException.ThrowIfNull(code);

            var mergedCodes = await GetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken) ?? "";
            var splitCodes = mergedCodes.Split(';');
            if (splitCodes.Contains(code))
            {
                var updatedCodes = new List<string>(splitCodes.Where(s => s != code));
                await ReplaceCodesAsync(user, updatedCodes, cancellationToken);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns how many recovery code are still valid for a user.
        /// </summary>
        /// <param name="user">The user who owns the recovery code.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The number of valid recovery codes for the user..</returns>
        public async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(user);

            var mergedCodes = await GetTokenAsync(
                user,
                InternalLoginProvider,
                RecoveryCodeTokenName,
                cancellationToken) ?? "";

            if (mergedCodes.Length > 0)
            {
#if NET8_0_OR_GREATER
                // Use Span for zero-allocation counting in .NET 8+
                return mergedCodes.AsSpan().Count(';') + 1;
#else
                return mergedCodes.Split(';').Length;
#endif
            }

            return 0;
        }

        /// <summary>
        /// Saves the current store.
        /// </summary>
        /// <param name="isAwait">Configure Await ?</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        private async Task SaveChanges(bool isAwait = false, CancellationToken cancellationToken = default)
        {
            if (AutoSaveChanges)
                await _session.SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(isAwait);
        }

        /// <summary>
        /// Throws if this class has been disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <summary>
        /// Dispose the store.
        /// </summary>
        /// <param name="disposing">Whether the class is actually disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_session != null)
                    _session = default;

                _disposed = true;
            }
        }

        /// <summary>
        /// Dispose the store.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
