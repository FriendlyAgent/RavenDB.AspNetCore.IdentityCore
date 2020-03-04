using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Stores
{

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user type.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    /// <typeparam name="TRoleClaim">The type of the class representing a role claim.</typeparam>
    public abstract class RavenUserStoreBase<TUser, TRole, TSession, TUserClaim, TUserLogin, TUserToken, TRoleClaim> :
            RavenUserOnlyStoreBase<TUser, TSession, TUserClaim, TUserLogin, TUserToken>,
            IUserRoleStore<TUser>
            where TUser : RavenIdentityUser
            where TRole : RavenIdentityRole
            where TSession : IAsyncDocumentSession
            where TUserClaim : RavenIdentityUserClaim, new()
            where TUserLogin : RavenIdentityUserLogin, new()
            where TUserToken : RavenIdentityUserToken, new()
            where TRoleClaim : RavenIdentityRoleClaim, new()
    {

        /// <summary>
        /// The <see cref="RavenIdentityRoleOptions"/> used to configure Raven Identity.
        /// </summary>
        public RavenIdentityRoleOptions<TRole, TSession> RavenRoleOptions { get; set; }

        private TSession _session { get; set; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        /// <param name="optionsAccessor">The configured <see cref="IdentityOptions"/>.</param>
        /// <param name="ravenUserOptionsAccessor">The configured <see cref="RavenIdentityUserOptions"/>.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenUserStoreBase(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<IdentityOptions> optionsAccessor = null,
            IOptions<RavenIdentityUserOptions<TUser, TSession>> ravenUserOptionsAccessor = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, optionsAccessor, ravenUserOptionsAccessor)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            RavenRoleOptions = ravenRoleOptionsAccessor?.Value ?? new RavenIdentityRoleOptions<TRole, TSession>();

            _session = session;
        }

        /// <summary>
        /// Adds the given <paramref name="normalizedRoleName"/> to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the role to.</param>
        /// <param name="normalizedRoleName">The role to add.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(normalizedRoleName))
                throw new ArgumentNullException(nameof(normalizedRoleName));

            var role = await RavenRoleOptions
                .Query
                .GetRoleByNameAsync(_session, normalizedRoleName, cancellationToken);

            if (role == null)
                throw new InvalidOperationException("Unable to find role.");

            user.Roles.Add(role.Id);
        }

        /// <summary>
        /// Retrieves the roles the specified <paramref name="user"/> is a member of.
        /// </summary>
        /// <param name="user">The user whose roles should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the roles the user is a member of.</returns>
        public virtual async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var roles = (await _session.LoadAsync<RavenIdentityRole>(user.Roles))
                .Select(a => a.Value.RoleName)
                .ToList();

            return await Task.FromResult<IList<string>>(roles);
        }

        /// <summary>
        /// Retrieves all users in the specified role.
        /// </summary>
        /// <param name="normalizedRoleName">The role whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that are in the specified role. 
        /// </returns>
        public virtual async Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(normalizedRoleName))
                throw new ArgumentNullException(nameof(normalizedRoleName));

            var role = await RavenRoleOptions
                .Query
                .GetRoleByNameAsync(_session, normalizedRoleName, cancellationToken);

            return await RavenUserOptions
                .Query
                .GetUsersInRoleAsync(_session, role.Id, cancellationToken);
        }

        /// <summary>
        /// Returns a flag indicating if the specified user is a member of the give <paramref name="normalizedRoleName"/>.
        /// </summary>
        /// <param name="user">The user whose role membership should be checked.</param>
        /// <param name="normalizedRoleName">The role to check membership of</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a flag indicating if the specified user is a member of the given group. If the 
        /// user is a member of the group the returned value with be true, otherwise it will be false.</returns>
        public virtual async Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(normalizedRoleName))
                throw new ArgumentNullException(nameof(normalizedRoleName));

            var role = await RavenRoleOptions
                .Query
                .GetRoleByNameAsync(_session, normalizedRoleName, cancellationToken);

            if (role != null)
                return user.Roles
                    .Contains(role.Id);

            return false;
        }

        /// <summary>
        /// Removes the given <paramref name="normalizedRoleName"/> from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the role from.</param>
        /// <param name="normalizedRoleName">The role to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual async Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrWhiteSpace(normalizedRoleName))
                throw new ArgumentNullException(nameof(normalizedRoleName));

            var role = await RavenRoleOptions
                .Query
                .GetRoleByNameAsync(_session, normalizedRoleName, cancellationToken);

            if (role != null)
                user.Roles.Remove(role.Id);
        }
    }
}
