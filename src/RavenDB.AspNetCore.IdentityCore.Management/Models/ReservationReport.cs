using System.Collections.Generic;

namespace RavenDB.AspNetCore.IdentityCore.Management.Models
{
    /// <summary>
    /// A health report of all identity Compare Exchange reservations.
    /// </summary>
    public class ReservationReport
    {
        /// <summary>
        /// Reservations that are correctly linked to an existing document.
        /// </summary>
        public List<ReservationEntry> Healthy { get; set; } = new();

        /// <summary>
        /// Reservations that point to a document ID that no longer exists.
        /// </summary>
        public List<ReservationEntry> Orphaned { get; set; } = new();

        /// <summary>
        /// Reservations that still have "pending" as the owner (creation was interrupted).
        /// </summary>
        public List<ReservationEntry> Pending { get; set; } = new();

        /// <summary>
        /// Documents that exist but have no corresponding reservation.
        /// </summary>
        public List<ReservationEntry> Missing { get; set; } = new();

        /// <summary>
        /// Total number of Compare Exchange reservations found.
        /// </summary>
        public int TotalReservations => Healthy.Count + Orphaned.Count + Pending.Count;

        /// <summary>
        /// Total number of documents found.
        /// </summary>
        public int TotalDocuments => Healthy.Count + Missing.Count;

        /// <summary>
        /// True if there are no orphaned, pending, or missing reservations.
        /// </summary>
        public bool IsHealthy => Orphaned.Count == 0 && Pending.Count == 0 && Missing.Count == 0;
    }
}
