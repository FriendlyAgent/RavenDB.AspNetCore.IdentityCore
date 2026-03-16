namespace RavenDB.AspNetCore.IdentityCore.Management.Models
{
    /// <summary>
    /// Represents a single Compare Exchange reservation and its health status.
    /// </summary>
    public class ReservationEntry
    {
        /// <summary>
        /// The type of reservation (UserName, Email, or RoleName).
        /// </summary>
        public ReservationType Type { get; set; }

        /// <summary>
        /// The full Compare Exchange key (e.g. "identity/users/by-name/johndoe").
        /// </summary>
        public string CompareExchangeKey { get; set; }

        /// <summary>
        /// The reserved normalized value (e.g. "johndoe", "john@example.com").
        /// </summary>
        public string ReservedValue { get; set; }

        /// <summary>
        /// The owner ID stored in the reservation (document ID or "pending").
        /// </summary>
        public string OwnerId { get; set; }

        /// <summary>
        /// The actual document ID found for this value, if any.
        /// </summary>
        public string DocumentId { get; set; }
    }
}
