using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provide error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        /// <param name="queryHandler">The query handler for role queries. If not provided, uses the default implementation.</param>
        /// <param name="loggerFactory">Optional logger factory for structured logging of Compare Exchange operations.</param>
        public RavenRoleStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<RavenIdentityRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null,
            IRoleQueryHandler<RavenIdentityRole, IAsyncDocumentSession> queryHandler = null,
            ILoggerFactory loggerFactory = null)
            : base(session, describer, ravenRoleOptionsAccessor, queryHandler, loggerFactory)
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
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provide error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        /// <param name="queryHandler">The query handler for role queries. If not provided, uses the default implementation.</param>
        /// <param name="loggerFactory">Optional logger factory for structured logging of Compare Exchange operations.</param>
        public RavenRoleStore(
            IAsyncDocumentSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, IAsyncDocumentSession>> ravenRoleOptionsAccessor = null,
            IRoleQueryHandler<TRole, IAsyncDocumentSession> queryHandler = null,
            ILoggerFactory loggerFactory = null)
            : base(session, describer, ravenRoleOptionsAccessor, queryHandler, loggerFactory)
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
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provide error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        /// <param name="queryHandler">The query handler for role queries. If not provided, uses the default implementation.</param>
        /// <param name="loggerFactory">Optional logger factory for structured logging of Compare Exchange operations.</param>
        public RavenRoleStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null,
            IRoleQueryHandler<TRole, TSession> queryHandler = null,
            ILoggerFactory loggerFactory = null)
            : base(session, describer, ravenRoleOptionsAccessor, queryHandler, loggerFactory)
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
        IRoleClaimStore<TRole>,
        IQueryableRoleStore<TRole>
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
        public IdentityErrorDescriber ErrorDescriber { get; set; }

        /// <summary>
        /// The <see cref="RavenIdentityRoleOptions"/> used to configure Raven Identity.
        /// </summary>
        public RavenIdentityRoleOptions<TRole, TSession> RavenRoleOptions { get; set; }

        /// <summary>
        /// The query handler used for querying roles.
        /// </summary>
        protected IRoleQueryHandler<TRole, TSession> QueryHandler { get; set; }

        /// <summary>
        /// The helper for managing role name reservations.
        /// </summary>
        private RoleNameReservationHelper RoleNameReservation { get; set; }

        private TSession _session;

        private bool _disposed;

        /// <summary>
        /// Gets an IQueryable of roles. NOT SUPPORTED - throws <see cref="NotSupportedException"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is required by the <see cref="IQueryableRoleStore{TRole}"/> interface from
        /// ASP.NET Core Identity, which is outside of our control. However, RavenDB uses an async
        /// document session model that is fundamentally incompatible with <see cref="IQueryable{T}"/>.
        /// Exposing a queryable here would bypass RavenDB's session lifetime management and could lead
        /// to unexpected behavior with deferred query execution.
        /// </para>
        /// <para>
        /// Instead, inject <see cref="IAsyncDocumentSession"/> and query roles directly:
        /// </para>
        /// <code>
        /// // Instead of: roleManager.Roles.Where(r => ...)
        /// // Use: session.Query&lt;TRole&gt;().Where(r => ...)
        /// </code>
        /// </remarks>
        /// <exception cref="NotSupportedException">Always thrown when accessed.</exception>
        public IQueryable<TRole> Roles =>
            throw new NotSupportedException(
                "IQueryable access via RoleManager.Roles is not supported. " +
                "RavenDB's async document session model is incompatible with IQueryable. " +
                "This property exists because the IQueryableRoleStore<TRole> interface requires it. " +
                "Inject IAsyncDocumentSession and use session.Query<TRole>() instead.");

        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleStore"/>.
        /// </summary>
        /// <param name="session">The <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to provide error messages.</param>
        /// <param name="ravenRoleOptionsAccessor">The configured <see cref="RavenIdentityRoleOptions"/>.</param>
        /// <param name="queryHandler">The query handler for role queries. If not provided, uses the default implementation.</param>
        /// <param name="loggerFactory">Optional logger factory for structured logging of Compare Exchange operations.</param>
        public RavenRoleStore(
            TSession session,
            IdentityErrorDescriber describer = null,
            IOptions<RavenIdentityRoleOptions<TRole, TSession>> ravenRoleOptionsAccessor = null,
            IRoleQueryHandler<TRole, TSession> queryHandler = null,
            ILoggerFactory loggerFactory = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            _session = session;

            ErrorDescriber = describer ?? new IdentityErrorDescriber();

            RavenRoleOptions = ravenRoleOptionsAccessor?.Value ?? new RavenIdentityRoleOptions<TRole, TSession>();

            QueryHandler = queryHandler ?? new DefaultRoleQueryHandler<TRole, TSession>(Microsoft.Extensions.Options.Options.Create(RavenRoleOptions));

            RoleNameReservation = new RoleNameReservationHelper(
                session.Advanced.DocumentStore,
                logger: loggerFactory?.CreateLogger<RoleNameReservationHelper>(),
                releaseRetryCount: RavenRoleOptions.ReservationReleaseRetryCount);

            AutoSaveChanges = RavenRoleOptions.AutoSaveChanges;
        }

        /// <summary>
        /// Creates an entity representing a role claim.
        /// </summary>
        /// <param name="role">The associated role.</param>
        /// <param name="claim">The associated claim.</param>
        /// <returns>The role claim entity.</returns>
        protected virtual TRoleClaim CreateRoleClaim(TRole role, Claim claim)
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

            ArgumentNullException.ThrowIfNull(role);

            ArgumentNullException.ThrowIfNull(claim);

            role.Claims.Add(CreateRoleClaim(role, claim));
            return Task.CompletedTask;
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

            ArgumentNullException.ThrowIfNull(role);

            var normalizedRoleName = role.NormalizedRoleName ?? role.RoleName;

            if (RavenRoleOptions.EnforceUniqueConstraints)
            {
                var reserved = await RoleNameReservation.TryReserveAsync(normalizedRoleName, "pending", cancellationToken);

                if (!reserved)
                {
                    return IdentityResult.Failed(ErrorDescriber.DuplicateRoleName(role.RoleName));
                }

                try
                {
                    await _session.StoreAsync(role, cancellationToken).ConfigureAwait(false);

                    var updated = await RoleNameReservation.TryUpdateAsync(normalizedRoleName, role.Id, cancellationToken);
                    if (!updated)
                    {
                        _session.Advanced.Evict(role);
                        await RoleNameReservation.TryReleaseAsync(normalizedRoleName, cancellationToken);
                        return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
                    }

                    await SaveChanges(cancellationToken: cancellationToken);
                }
                catch (Exception)
                {
                    _session.Advanced.Evict(role);
                    await RoleNameReservation.TryReleaseAsync(normalizedRoleName, cancellationToken);
                    throw;
                }
            }
            else
            {
                await _session.StoreAsync(role, cancellationToken).ConfigureAwait(false);
                await SaveChanges(cancellationToken: cancellationToken);
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

            ArgumentNullException.ThrowIfNull(role);

            var normalizedRoleName = role.NormalizedRoleName ?? role.RoleName;

            try
            {
                _session.Delete(role);
                await SaveChanges(cancellationToken: cancellationToken);

                if (RavenRoleOptions.EnforceUniqueConstraints)
                {
                    await RoleNameReservation.TryReleaseAsync(normalizedRoleName, cancellationToken);
                }
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

            ArgumentNullException.ThrowIfNull(roleId);

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

            ArgumentNullException.ThrowIfNull(normalizedRoleName);

            return await QueryHandler
                .GetByNameAsync(_session, normalizedRoleName, cancellationToken);
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

            ArgumentNullException.ThrowIfNull(role);

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

            ArgumentNullException.ThrowIfNull(role);

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

            ArgumentNullException.ThrowIfNull(role);

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

            ArgumentNullException.ThrowIfNull(role);

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
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(role);

            ArgumentNullException.ThrowIfNull(claim);

            var userClaim = role.Claims
                .SingleOrDefault(a => a.Equals(claim));

            if (userClaim != null)
                role.Claims.Remove(userClaim);

            return Task.CompletedTask;
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

            ArgumentNullException.ThrowIfNull(role);

            role.NormalizedRoleName = normalizedName ?? throw new ArgumentNullException(nameof(normalizedName));

            return Task.CompletedTask;
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

            ArgumentNullException.ThrowIfNull(role);

            role.RoleName = roleName;

            return Task.CompletedTask;
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

            ArgumentNullException.ThrowIfNull(role);

            // Check what changed in this session
            var changes = _session.Advanced.WhatChanged();
            var hasRoleChanged = changes.TryGetValue(role.Id, out var roleChanges);

            if (!hasRoleChanged || roleChanges == null || roleChanges.Length == 0)
            {
                // No changes to this document
                return IdentityResult.Success;
            }

            // Check for role name change
            var nameChange = roleChanges.FirstOrDefault(c =>
                c.FieldName == nameof(role.NormalizedRoleName) || c.FieldName == nameof(role.RoleName));

            if (nameChange == null || !RavenRoleOptions.EnforceUniqueConstraints)
            {
                // Role name didn't change or unique constraints disabled, just save normally
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

            // Role name changed, need to update compare-exchange reservation
            var oldNormalizedName = nameChange.FieldOldValue?.ToString();
            var currentNormalizedName = role.NormalizedRoleName ?? role.RoleName;

            // Check if it's actually a meaningful change (not just case change)
            if (string.IsNullOrEmpty(oldNormalizedName) ||
                string.Equals(oldNormalizedName, currentNormalizedName, StringComparison.OrdinalIgnoreCase))
            {
                // Just a case change or invalid old value, save normally
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

            // Real name change detected, update compare-exchange
            var reserved = await RoleNameReservation.TryReserveAsync(currentNormalizedName, role.Id, cancellationToken);

            if (!reserved)
            {
                return IdentityResult.Failed(ErrorDescriber.DuplicateRoleName(role.RoleName));
            }

            try
            {
                role.ConcurrencyStamp = Guid.NewGuid().ToString();
                await SaveChanges(cancellationToken: cancellationToken);

                await RoleNameReservation.TryReleaseAsync(oldNormalizedName, cancellationToken);
            }
            catch (Exception)
            {
                await RoleNameReservation.TryReleaseAsync(currentNormalizedName, cancellationToken);
                throw;
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