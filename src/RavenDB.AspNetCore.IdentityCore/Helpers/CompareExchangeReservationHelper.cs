using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Helpers
{
    /// <summary>
    /// Base class for managing Compare Exchange reservations in RavenDB.
    /// Provides atomic reserve, update, and release operations for unique constraints.
    /// </summary>
    public abstract class CompareExchangeReservationHelper
    {
        private readonly IDocumentStore _documentStore;
        private readonly ILogger _logger;
        private readonly string _reservationType;
        private readonly int _releaseRetryCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompareExchangeReservationHelper"/> class.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        /// <param name="reservationType">A human-readable label for log messages (e.g. "username", "role name", "email").</param>
        /// <param name="logger">Optional logger for tracking reservation operations.</param>
        /// <param name="releaseRetryCount">Number of retry attempts when releasing a reservation fails due to concurrency. Set to 0 to disable retries. Defaults to 1.</param>
        protected CompareExchangeReservationHelper(
            IDocumentStore documentStore,
            string reservationType,
            ILogger logger = null,
            int releaseRetryCount = 1)
        {
            _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
            _reservationType = reservationType;
            _logger = logger;
            _releaseRetryCount = releaseRetryCount >= 0 ? releaseRetryCount : throw new ArgumentOutOfRangeException(nameof(releaseRetryCount), "Must be >= 0.");
        }

        /// <summary>
        /// Builds the Compare Exchange key for the given normalized value.
        /// </summary>
        /// <param name="normalizedValue">The normalized value to build a key for.</param>
        /// <returns>The full Compare Exchange key.</returns>
        protected abstract string BuildKey(string normalizedValue);

        /// <summary>
        /// Tries to reserve a value for the specified owner ID.
        /// </summary>
        /// <param name="normalizedValue">The normalized value to reserve.</param>
        /// <param name="ownerId">The owner ID to associate with this reservation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the reservation was successful, false if already taken.</returns>
        public virtual async Task<bool> TryReserveAsync(
            string normalizedValue,
            string ownerId,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(normalizedValue);
            var operation = new PutCompareExchangeValueOperation<string>(key, ownerId, 0);
            var result = await _documentStore.Operations.SendAsync(operation, token: cancellationToken);

            if (result.Successful)
            {
                _logger?.LogDebug("Successfully reserved {ReservationType} '{Value}' for ID '{OwnerId}'",
                    _reservationType, normalizedValue, ownerId);
            }
            else
            {
                _logger?.LogWarning("Failed to reserve {ReservationType} '{Value}' for ID '{OwnerId}'. Already taken by ID '{ExistingOwnerId}'",
                    _reservationType, normalizedValue, ownerId, result.Value);
            }

            return result.Successful;
        }

        /// <summary>
        /// Updates an existing reservation to point to a new owner ID.
        /// </summary>
        /// <param name="normalizedValue">The normalized value.</param>
        /// <param name="ownerId">The new owner ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the update was successful, false otherwise.</returns>
        public virtual async Task<bool> TryUpdateAsync(
            string normalizedValue,
            string ownerId,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(normalizedValue);

            var currentValue = await _documentStore.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(key),
                token: cancellationToken);

            if (currentValue == null)
            {
                _logger?.LogWarning("Failed to update {ReservationType} reservation '{Value}' for ID '{OwnerId}'. Reservation does not exist.",
                    _reservationType, normalizedValue, ownerId);
                return false;
            }

            var updateOperation = new PutCompareExchangeValueOperation<string>(key, ownerId, currentValue.Index);
            var result = await _documentStore.Operations.SendAsync(updateOperation, token: cancellationToken);

            if (result.Successful)
            {
                _logger?.LogDebug("Successfully updated {ReservationType} reservation '{Value}' to ID '{OwnerId}'",
                    _reservationType, normalizedValue, ownerId);
            }
            else
            {
                _logger?.LogWarning("Failed to update {ReservationType} reservation '{Value}' for ID '{OwnerId}'. Concurrency conflict.",
                    _reservationType, normalizedValue, ownerId);
            }

            return result.Successful;
        }

        /// <summary>
        /// Releases a reservation.
        /// </summary>
        /// <param name="normalizedValue">The normalized value to release.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True if the release was successful, false otherwise.</returns>
        public virtual async Task<bool> TryReleaseAsync(
            string normalizedValue,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(normalizedValue);

            for (int attempt = 0; attempt <= _releaseRetryCount; attempt++)
            {
                var currentValue = await _documentStore.Operations.SendAsync(
                    new GetCompareExchangeValueOperation<string>(key),
                    token: cancellationToken);

                if (currentValue == null)
                {
                    if (attempt == 0)
                    {
                        _logger?.LogWarning("Failed to release {ReservationType} reservation '{Value}'. Reservation does not exist.",
                            _reservationType, normalizedValue);
                        return false;
                    }

                    // Reservation disappeared between attempts — treat as success
                    _logger?.LogDebug("Reservation {ReservationType} '{Value}' no longer exists on retry attempt {Attempt}. Treating as released.",
                        _reservationType, normalizedValue, attempt);
                    return true;
                }

                var deleteOperation = new DeleteCompareExchangeValueOperation<string>(key, currentValue.Index);
                var result = await _documentStore.Operations.SendAsync(deleteOperation, token: cancellationToken);

                if (result.Successful)
                {
                    _logger?.LogDebug("Successfully released {ReservationType} reservation '{Value}'",
                        _reservationType, normalizedValue);
                    return true;
                }

                _logger?.LogWarning("Attempt {Attempt} to release {ReservationType} reservation '{Value}' failed due to concurrency.{RetryMessage}",
                    attempt + 1, _reservationType, normalizedValue,
                    attempt < _releaseRetryCount ? " Retrying." : " No retries remaining.");
            }

            _logger?.LogError("CRITICAL: Failed to release {ReservationType} reservation '{Value}' after {Attempts} attempt(s). The reservation is orphaned.",
                _reservationType, normalizedValue, _releaseRetryCount + 1);

            return false;
        }
    }
}
