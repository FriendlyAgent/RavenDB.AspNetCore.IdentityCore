using Microsoft.Extensions.Logging;
using Raven.Client.Documents;

namespace RavenDB.AspNetCore.IdentityCore.Helpers
{
    /// <summary>
    /// Helper class for managing role name reservations using Compare Exchange.
    /// </summary>
    public class RoleNameReservationHelper : CompareExchangeReservationHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoleNameReservationHelper"/> class.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        /// <param name="logger">Optional logger for tracking reservation operations.</param>
        /// <param name="releaseRetryCount">Number of retry attempts when releasing a reservation fails. Defaults to 1.</param>
        public RoleNameReservationHelper(IDocumentStore documentStore, ILogger<RoleNameReservationHelper> logger = null, int releaseRetryCount = 1)
            : base(documentStore, "role name", logger, releaseRetryCount)
        {
        }

        /// <inheritdoc />
        protected override string BuildKey(string normalizedValue)
            => CompareExchangeKeys.ForRoleName(normalizedValue);
    }
}
