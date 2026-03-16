using Microsoft.Extensions.Logging;
using Raven.Client.Documents;

namespace RavenDB.AspNetCore.IdentityCore.Helpers
{
    /// <summary>
    /// Helper class for managing username reservations using Compare Exchange.
    /// </summary>
    public class UserNameReservationHelper : CompareExchangeReservationHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserNameReservationHelper"/> class.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        /// <param name="logger">Optional logger for tracking reservation operations.</param>
        /// <param name="releaseRetryCount">Number of retry attempts when releasing a reservation fails. Defaults to 1.</param>
        public UserNameReservationHelper(IDocumentStore documentStore, ILogger<UserNameReservationHelper> logger = null, int releaseRetryCount = 1)
            : base(documentStore, "username", logger, releaseRetryCount)
        {
        }

        /// <inheritdoc />
        protected override string BuildKey(string normalizedValue)
            => CompareExchangeKeys.ForUserName(normalizedValue);
    }
}
