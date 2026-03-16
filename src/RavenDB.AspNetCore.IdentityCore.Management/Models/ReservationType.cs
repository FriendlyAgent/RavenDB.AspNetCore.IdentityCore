namespace RavenDB.AspNetCore.IdentityCore.Management.Models
{
    /// <summary>
    /// The type of Compare Exchange reservation.
    /// </summary>
    public enum ReservationType
    {
        /// <summary>
        /// A username reservation (identity/users/by-name/).
        /// </summary>
        UserName,

        /// <summary>
        /// An email reservation (identity/emails/by-value/).
        /// </summary>
        Email,

        /// <summary>
        /// A role name reservation (identity/roles/by-name/).
        /// </summary>
        RoleName
    }
}
