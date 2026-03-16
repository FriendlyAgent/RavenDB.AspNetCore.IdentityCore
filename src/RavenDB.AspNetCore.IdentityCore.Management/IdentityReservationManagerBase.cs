using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using RavenDB.AspNetCore.IdentityCore.Management.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Management
{
    /// <summary>
    /// Base class for identity reservation managers. Provides shared Compare Exchange
    /// operations and report/repair/migration logic.
    /// </summary>
    public abstract class IdentityReservationManagerBase
    {
        /// <summary>
        /// The RavenDB document store.
        /// </summary>
        protected readonly IDocumentStore _store;

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityReservationManagerBase"/>.
        /// </summary>
        /// <param name="store">The RavenDB document store.</param>
        protected IdentityReservationManagerBase(IDocumentStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        // ==================== DIAGNOSTICS ====================

        /// <summary>
        /// Scans all identity Compare Exchange reservations and documents, returning a full health report.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ReservationReport"/> with categorized entries.</returns>
        public async Task<ReservationReport> GetReportAsync(CancellationToken cancellationToken = default)
        {
            var report = new ReservationReport();

            var reservations = new List<(string Key, string Value, long Index)>();
            await CollectReservationsAsync(reservations, cancellationToken);

            var documentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await PopulateDocumentDataAsync(documentIds, expectedKeys, cancellationToken);

            // Classify each reservation
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reservation in reservations)
            {
                seenKeys.Add(reservation.Key);

                var entry = new ReservationEntry
                {
                    Type = GetReservationType(reservation.Key),
                    CompareExchangeKey = reservation.Key,
                    ReservedValue = ExtractValue(reservation.Key),
                    OwnerId = reservation.Value
                };

                if (string.Equals(reservation.Value, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    if (expectedKeys.TryGetValue(reservation.Key, out var docId))
                    {
                        entry.DocumentId = docId;
                    }
                    report.Pending.Add(entry);
                }
                else if (!documentIds.Contains(reservation.Value))
                {
                    report.Orphaned.Add(entry);
                }
                else
                {
                    entry.DocumentId = reservation.Value;
                    report.Healthy.Add(entry);
                }
            }

            // Find documents without reservations
            foreach (var kvp in expectedKeys)
            {
                if (!seenKeys.Contains(kvp.Key))
                {
                    report.Missing.Add(new ReservationEntry
                    {
                        Type = GetReservationType(kvp.Key),
                        CompareExchangeKey = kvp.Key,
                        ReservedValue = ExtractValue(kvp.Key),
                        DocumentId = kvp.Value
                    });
                }
            }

            return report;
        }

        // ==================== REPAIR ====================

        /// <summary>
        /// Removes orphaned reservations (reservation exists but the document it points to doesn't).
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of orphaned reservations removed.</returns>
        public async Task<int> RemoveOrphanedReservationsAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetReportAsync(cancellationToken);
            var removed = 0;

            foreach (var entry in report.Orphaned)
            {
                if (await TryDeleteReservationAsync(entry.CompareExchangeKey, cancellationToken))
                    removed++;
            }

            return removed;
        }

        /// <summary>
        /// Resolves pending reservations. If the document exists, updates to the real ID. Otherwise removes the reservation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of pending reservations resolved.</returns>
        public async Task<int> RepairPendingReservationsAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetReportAsync(cancellationToken);
            var repaired = 0;

            foreach (var entry in report.Pending)
            {
                if (!string.IsNullOrEmpty(entry.DocumentId))
                {
                    if (await TryUpdateReservationAsync(entry.CompareExchangeKey, entry.DocumentId, cancellationToken))
                        repaired++;
                }
                else
                {
                    if (await TryDeleteReservationAsync(entry.CompareExchangeKey, cancellationToken))
                        repaired++;
                }
            }

            return repaired;
        }

        /// <summary>
        /// Creates missing reservations for documents that don't have them.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of reservations created.</returns>
        public async Task<int> CreateMissingReservationsAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetReportAsync(cancellationToken);
            var created = 0;

            foreach (var entry in report.Missing)
            {
                var operation = new PutCompareExchangeValueOperation<string>(
                    entry.CompareExchangeKey, entry.DocumentId, 0);
                var result = await _store.Operations.SendAsync(operation, token: cancellationToken);

                if (result.Successful)
                    created++;
            }

            return created;
        }

        /// <summary>
        /// Runs all repair operations: removes orphaned, fixes pending, creates missing.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A report of what was found before repair.</returns>
        public async Task<ReservationReport> RepairAllAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetReportAsync(cancellationToken);

            foreach (var entry in report.Orphaned)
            {
                await TryDeleteReservationAsync(entry.CompareExchangeKey, cancellationToken);
            }

            foreach (var entry in report.Pending)
            {
                if (!string.IsNullOrEmpty(entry.DocumentId))
                    await TryUpdateReservationAsync(entry.CompareExchangeKey, entry.DocumentId, cancellationToken);
                else
                    await TryDeleteReservationAsync(entry.CompareExchangeKey, cancellationToken);
            }

            foreach (var entry in report.Missing)
            {
                var operation = new PutCompareExchangeValueOperation<string>(
                    entry.CompareExchangeKey, entry.DocumentId, 0);
                await _store.Operations.SendAsync(operation, token: cancellationToken);
            }

            return report;
        }

        // ==================== MIGRATION ====================

        /// <summary>
        /// Creates reservations for all existing documents. Use when enabling EnforceUniqueConstraints on existing data.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A report showing the state before migration (Missing entries = created).</returns>
        public async Task<ReservationReport> EnableConstraintsAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetReportAsync(cancellationToken);

            foreach (var entry in report.Missing)
            {
                var operation = new PutCompareExchangeValueOperation<string>(
                    entry.CompareExchangeKey, entry.DocumentId, 0);
                await _store.Operations.SendAsync(operation, token: cancellationToken);
            }

            foreach (var entry in report.Pending)
            {
                if (!string.IsNullOrEmpty(entry.DocumentId))
                    await TryUpdateReservationAsync(entry.CompareExchangeKey, entry.DocumentId, cancellationToken);
                else
                    await TryDeleteReservationAsync(entry.CompareExchangeKey, cancellationToken);
            }

            return report;
        }

        /// <summary>
        /// Removes all identity Compare Exchange reservations. Use when disabling EnforceUniqueConstraints.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of reservations removed.</returns>
        public async Task<int> DisableConstraintsAsync(CancellationToken cancellationToken = default)
        {
            var reservations = new List<(string Key, string Value, long Index)>();
            await CollectReservationsAsync(reservations, cancellationToken);
            var removed = 0;

            foreach (var reservation in reservations)
            {
                if (await TryDeleteReservationAsync(reservation.Key, cancellationToken))
                    removed++;
            }

            return removed;
        }

        // ==================== ABSTRACT METHODS ====================

        /// <summary>
        /// Collects all Compare Exchange reservations managed by this instance.
        /// </summary>
        /// <param name="results">The list to add reservations to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task CollectReservationsAsync(
            List<(string Key, string Value, long Index)> results,
            CancellationToken cancellationToken);

        /// <summary>
        /// Populates document IDs and expected Compare Exchange keys from the database.
        /// </summary>
        /// <param name="documentIds">The set of document IDs to populate.</param>
        /// <param name="expectedKeys">The dictionary of expected CE key to document ID to populate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task PopulateDocumentDataAsync(
            HashSet<string> documentIds,
            Dictionary<string, string> expectedKeys,
            CancellationToken cancellationToken);

        // ==================== PROTECTED HELPERS ====================

        /// <summary>
        /// Collects all Compare Exchange values with the specified prefix.
        /// </summary>
        protected async Task CollectReservationsByPrefixAsync(
            string prefix,
            List<(string Key, string Value, long Index)> results,
            CancellationToken cancellationToken)
        {
            var start = 0;
            const int pageSize = 25;

            while (true)
            {
                var operation = new GetCompareExchangeValuesOperation<string>(prefix, start, pageSize);
                var values = await _store.Operations.SendAsync(operation, token: cancellationToken);

                if (values == null || values.Count == 0)
                    break;

                foreach (var kvp in values)
                {
                    results.Add((kvp.Key, kvp.Value.Value, kvp.Value.Index));
                }

                if (values.Count < pageSize)
                    break;

                start += pageSize;
            }
        }

        // ==================== PRIVATE HELPERS ====================

        private async Task<bool> TryDeleteReservationAsync(string key, CancellationToken cancellationToken)
        {
            var current = await _store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(key),
                token: cancellationToken);

            if (current == null)
                return false;

            var result = await _store.Operations.SendAsync(
                new DeleteCompareExchangeValueOperation<string>(key, current.Index),
                token: cancellationToken);

            return result.Successful;
        }

        private async Task<bool> TryUpdateReservationAsync(string key, string newValue, CancellationToken cancellationToken)
        {
            var current = await _store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(key),
                token: cancellationToken);

            if (current == null)
                return false;

            var result = await _store.Operations.SendAsync(
                new PutCompareExchangeValueOperation<string>(key, newValue, current.Index),
                token: cancellationToken);

            return result.Successful;
        }

        private static ReservationType GetReservationType(string key)
        {
            if (key.StartsWith(CompareExchangeKeys.UserNamePrefix, StringComparison.OrdinalIgnoreCase))
                return ReservationType.UserName;
            if (key.StartsWith(CompareExchangeKeys.EmailPrefix, StringComparison.OrdinalIgnoreCase))
                return ReservationType.Email;
            if (key.StartsWith(CompareExchangeKeys.RoleNamePrefix, StringComparison.OrdinalIgnoreCase))
                return ReservationType.RoleName;

            return ReservationType.UserName; // fallback
        }

        private static string ExtractValue(string key)
        {
            if (key.StartsWith(CompareExchangeKeys.UserNamePrefix, StringComparison.OrdinalIgnoreCase))
                return key.Substring(CompareExchangeKeys.UserNamePrefix.Length);
            if (key.StartsWith(CompareExchangeKeys.EmailPrefix, StringComparison.OrdinalIgnoreCase))
                return key.Substring(CompareExchangeKeys.EmailPrefix.Length);
            if (key.StartsWith(CompareExchangeKeys.RoleNamePrefix, StringComparison.OrdinalIgnoreCase))
                return key.Substring(CompareExchangeKeys.RoleNamePrefix.Length);

            return key;
        }
    }
}
