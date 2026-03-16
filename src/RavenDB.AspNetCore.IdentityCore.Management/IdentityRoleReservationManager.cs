using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Management
{
    /// <summary>
    /// Provides diagnostics, repair, and migration tools for role identity Compare Exchange reservations (role name).
    /// Uses the default <see cref="RavenIdentityRole"/> type.
    /// </summary>
    public class IdentityRoleReservationManager
        : IdentityRoleReservationManager<RavenIdentityRole>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityRoleReservationManager"/> class.
        /// </summary>
        /// <param name="store">The RavenDB document store.</param>
        public IdentityRoleReservationManager(IDocumentStore store) : base(store) { }
    }

    /// <summary>
    /// Provides diagnostics, repair, and migration tools for role identity Compare Exchange reservations (role name).
    /// </summary>
    /// <typeparam name="TRole">The type representing a role.</typeparam>
    public class IdentityRoleReservationManager<TRole>
        : IdentityReservationManagerBase
        where TRole : RavenIdentityRole
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityRoleReservationManager{TRole}"/> class.
        /// </summary>
        /// <param name="store">The RavenDB document store.</param>
        public IdentityRoleReservationManager(IDocumentStore store) : base(store) { }

        /// <inheritdoc />
        protected override async Task CollectReservationsAsync(
            List<(string Key, string Value, long Index)> results,
            CancellationToken cancellationToken)
        {
            await CollectReservationsByPrefixAsync(CompareExchangeKeys.RoleNamePrefix, results, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task PopulateDocumentDataAsync(
            HashSet<string> documentIds,
            Dictionary<string, string> expectedKeys,
            CancellationToken cancellationToken)
        {
            var roles = await GetAllRolesAsync(cancellationToken);

            foreach (var role in roles)
            {
                documentIds.Add(role.Id);

                var normalizedRoleName = role.NormalizedRoleName ?? role.RoleName;
                if (!string.IsNullOrWhiteSpace(normalizedRoleName))
                {
                    var key = CompareExchangeKeys.ForRoleName(normalizedRoleName);
                    expectedKeys[key] = role.Id;
                }
            }
        }

        private async Task<List<TRole>> GetAllRolesAsync(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var roles = new List<TRole>();

            await using var stream = await session.Advanced.StreamAsync(
                session.Query<TRole>(), cancellationToken);

            while (await stream.MoveNextAsync())
            {
                roles.Add(stream.Current.Document);
            }

            return roles;
        }
    }
}
