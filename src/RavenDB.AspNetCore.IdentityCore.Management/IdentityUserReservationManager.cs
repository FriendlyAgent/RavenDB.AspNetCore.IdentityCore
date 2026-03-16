using Raven.Client.Documents;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Management
{
    /// <summary>
    /// Provides diagnostics, repair, and migration tools for user identity Compare Exchange reservations (username and email).
    /// Uses the default <see cref="RavenIdentityUser"/> type.
    /// </summary>
    public class IdentityUserReservationManager
        : IdentityUserReservationManager<RavenIdentityUser>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityUserReservationManager"/> class.
        /// </summary>
        /// <param name="store">The RavenDB document store.</param>
        public IdentityUserReservationManager(IDocumentStore store) : base(store) { }
    }

    /// <summary>
    /// Provides diagnostics, repair, and migration tools for user identity Compare Exchange reservations (username and email).
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class IdentityUserReservationManager<TUser>
        : IdentityReservationManagerBase
        where TUser : RavenIdentityUser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityUserReservationManager{TUser}"/> class.
        /// </summary>
        /// <param name="store">The RavenDB document store.</param>
        public IdentityUserReservationManager(IDocumentStore store) : base(store) { }

        /// <inheritdoc />
        protected override async Task CollectReservationsAsync(
            List<(string Key, string Value, long Index)> results,
            CancellationToken cancellationToken)
        {
            await CollectReservationsByPrefixAsync(CompareExchangeKeys.UserNamePrefix, results, cancellationToken);
            await CollectReservationsByPrefixAsync(CompareExchangeKeys.EmailPrefix, results, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task PopulateDocumentDataAsync(
            HashSet<string> documentIds,
            Dictionary<string, string> expectedKeys,
            CancellationToken cancellationToken)
        {
            var users = await GetAllUsersAsync(cancellationToken);

            foreach (var user in users)
            {
                documentIds.Add(user.Id);

                var normalizedUserName = user.NormalizedUserName ?? user.UserName;
                if (!string.IsNullOrWhiteSpace(normalizedUserName))
                {
                    var key = CompareExchangeKeys.ForUserName(normalizedUserName);
                    expectedKeys[key] = user.Id;
                }

                var normalizedEmail = user.Email?.NormalizedEmail ?? user.Email?.Email;
                if (!string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    var key = CompareExchangeKeys.ForEmail(normalizedEmail);
                    expectedKeys[key] = user.Id;
                }
            }
        }

        private async Task<List<TUser>> GetAllUsersAsync(CancellationToken cancellationToken)
        {
            using var session = _store.OpenAsyncSession();
            var users = new List<TUser>();

            await using var stream = await session.Advanced.StreamAsync(
                session.Query<TUser>(), cancellationToken);

            while (await stream.MoveNextAsync())
            {
                users.Add(stream.Current.Document);
            }

            return users;
        }
    }
}
