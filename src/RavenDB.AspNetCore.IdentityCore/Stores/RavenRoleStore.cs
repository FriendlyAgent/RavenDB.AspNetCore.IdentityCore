using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using RavenDB.AspNetCore.IdentityCore.Entities.UniqueConstraints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Creates a new instance of a persistence store for roles, using the default implementation
    /// </summary>
    public class RavenRoleStore
        : RavenRoleStore<RavenIdentityRole>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenRoleStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<RavenIdentityRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for roles.
    /// </summary>
    /// <typeparam name="TRole">The type of the class representing a role.</typeparam>
    public class RavenRoleStore<TRole>
        : RavenRoleStore<TRole, IAsyncDocumentSession>
        where TRole : RavenIdentityRole
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenRoleStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for roles.
    /// </summary>
    /// <typeparam name="TRole">The type of the class representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenRoleStore<TRole, TSession>
        : RavenRoleStore<TRole, TSession, RavenIdentityRoleClaim>,
        IRoleClaimStore<TRole>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenRoleStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null)
            : base(session, describer, ravenRoleOptionsAccessor)
        {
        }
    }

    /// <summary>
    /// Creates a new instance of a persistence store for roles.
    /// </summary>
    /// <typeparam name="TRole">The type of the class representing a role.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    /// <typeparam name="TRoleClaim">The type of the class representing a role claim.</typeparam>
    public class RavenRoleStore<TRole, TSession, TRoleClaim> :
        IRoleClaimStore<TRole>
        where TRole : RavenIdentityRole
        where TSession : IAsyncDocumentSession
        where TRoleClaim : RavenIdentityRoleClaim, new()
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
        private IdentityErrorDescriber ErrorDescriber { get; set; }

        /// <summary>
        /// The <see cref="RavenIdentityRoleOptions"/> used to configure Raven Identity.
        /// </summary>
        private RavenIdentityRoleOptions<TRole, TSession> RavenRoleOptions { get; set; }

        private TSession _session;

        private bool _disposed;

        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        public RavenRoleStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            _session = session;

            ErrorDescriber = describer ?? new IdentityErrorDescriber();

            RavenRoleOptions = ravenRoleOptionsAccessor?.Value ?? new RavenIdentityRoleOptions<TRole, TSession>();
        }

        /// <summary>
        /// Creates a entity representing a role claim.
        /// </summary>
        /// <param name="role">The associated role.</param>
        /// <param name="claim">The associated claim.</param>
        /// <returns>The role claim entity.</returns>
        protected virtual TRoleClaim CreaterRoleClaim(TRole role, Claim claim)
        {
            if (role.Claims.Any(x => x.Equals(claim)))
                throw new InvalidOperationException("Claim already exists.");

            return new TRoleClaim
            {
                ClaimType = claim.Type,
                ClaimValue = claim.Value
            };
        }

        /// <summary>
        /// Adds the <paramref name="claim"/> given to the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to add the claim to.</param>
        /// <param name="claim">The claim to add to the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            if (claim == null)
                throw new ArgumentNullException(nameof(claim));

            role.Claims.Add(CreaterRoleClaim(role, claim));
            return Task.FromResult(false);
        }

        /// <summary>
        /// Creates a constraint on the role name.
        /// </summary>
        /// <param name="role">The role for which to create the constraint.</param>
        /// <returns></returns>
        public static UniqueRoleName ToRoleNameConstraint(RavenIdentityRole role)
        {
            return new UniqueRoleName(role.RoleName);
        }

        /// <summary>
        /// Creates a new role in a store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to create in the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public virtual async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            var previousOptimisticConcurrency = _session.Advanced.UseOptimisticConcurrency;
            var uniqueRoleNameConstraint = ToRoleNameConstraint(role);
            try
            {
                _session.Advanced.UseOptimisticConcurrency = true;

                await _session
                    .StoreAsync(uniqueRoleNameConstraint, cancellationToken)
                    .ConfigureAwait(false);

                await SaveChanges(cancellationToken: cancellationToken);

                await _session
                    .StoreAsync(role, cancellationToken)
                    .ConfigureAwait(false);

                uniqueRoleNameConstraint.RelationId = role.Id;

                await SaveChanges(cancellationToken: cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _session.Advanced.Evict(uniqueRoleNameConstraint);
                _session.Advanced.Evict(role);

                if (ex.Message.Contains(uniqueRoleNameConstraint.Id)) // RoleName error
                {
                    return IdentityResult
                        .Failed(ErrorDescriber.DuplicateRoleName(role.RoleName));
                }

                return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
            }
            catch (NonUniqueObjectException ex)
            {
                _session.Advanced.Evict(uniqueRoleNameConstraint);
                _session.Advanced.Evict(role);

                if (ex.Message
                    .Contains(uniqueRoleNameConstraint.Id)) // RoleName error
                {
                    return IdentityResult
                        .Failed(ErrorDescriber.DuplicateRoleName(role.RoleName));
                }

                throw;
            }
            finally
            {
                _session.Advanced.UseOptimisticConcurrency = previousOptimisticConcurrency;
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// Deletes a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to delete from the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public virtual async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            try
            {
                var uniqueRoleNameConstraint = ToRoleNameConstraint(role);

                _session.Delete(uniqueRoleNameConstraint.Id);
                _session.Delete(role);

                await SaveChanges(cancellationToken: cancellationToken);
            }
            catch (ConcurrencyException)
            {
                return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// Finds the role who has the specified ID as an asynchronous operation.
        /// </summary>
        /// <param name="roleId">The role ID to look for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
        public virtual Task<TRole> FindByIdAsync(string roleId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (roleId == null)
                throw new ArgumentNullException(nameof(roleId));

            return _session.LoadAsync<TRole>(roleId, cancellationToken);
        }

        /// <summary>
        /// Finds the role who has the specified normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="normalizedRoleName">The normalized role name to look for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that result of the look up.</returns>
        public virtual async Task<TRole> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (normalizedRoleName == null)
                throw new ArgumentNullException(nameof(normalizedRoleName));

            return await RavenRoleOptions
                .Query
                .GetRoleByNameAsync(_session, normalizedRoleName, cancellationToken);
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="role"/> as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a role.</returns>
        public virtual Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            var claims = role.Claims.
                Select(claim => claim
                    .ToClaim())
                .ToList();

            return Task.FromResult<IList<Claim>>(claims);
        }

        /// <summary>
        /// Get a role's normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose normalized name should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
        public virtual Task<string> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            return Task.FromResult(role.NormalizedRoleName);
        }

        /// <summary>
        /// Gets the ID for a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose ID should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the ID of the role.</returns>
        public virtual Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            return Task.FromResult(role.Id);
        }

        /// <summary>
        /// Gets the name of a role from the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose name should be returned.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the name of the role.</returns>
        public virtual Task<string> GetRoleNameAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            return Task.FromResult(role.RoleName);
        }

        /// <summary>
        /// Removes the <paramref name="claim"/> given from the specified <paramref name="role"/>.
        /// </summary>
        /// <param name="role">The role to remove the claim from.</param>
        /// <param name="claim">The claim to remove from the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken = default)
        {
            var userClaim = role.Claims
                .SingleOrDefault(a => a.Equals(claim));

            if (userClaim != null)
                role.Claims.Remove(userClaim);

            return Task.FromResult(0);
        }

        /// <summary>
        /// Set a role's normalized name as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose normalized name should be set.</param>
        /// <param name="normalizedName">The normalized name to set</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetNormalizedRoleNameAsync(TRole role, string normalizedName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            role.NormalizedRoleName = normalizedName ?? throw new ArgumentNullException(nameof(normalizedName));

            return Task.FromResult(0);
        }

        /// <summary>
        /// Sets the name of a role in the store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role whose name should be set.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public virtual Task SetRoleNameAsync(TRole role, string roleName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            role.RoleName = roleName;

            return Task.FromResult(0);
        }

        /// <summary>
        /// Updates a role in a store as an asynchronous operation.
        /// </summary>
        /// <param name="role">The role to update in the store.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the <see cref="IdentityResult"/> of the asynchronous query.</returns>
        public virtual async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null)
                throw new ArgumentNullException(nameof(role));

            try
            {
                role.ConcurrencyStamp = Guid.NewGuid().ToString();

                await SaveChanges(cancellationToken: cancellationToken);
            }
            catch (ConcurrencyException)
            {
                return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
            }

            return IdentityResult.Success;
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
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
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