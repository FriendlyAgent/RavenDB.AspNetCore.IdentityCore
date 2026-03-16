using System;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Constants for Compare Exchange key patterns used for unique constraints in the identity system.
    /// Keys are prefixed with 'identity/' to avoid conflicts with other systems using the same RavenDB instance.
    /// </summary>
    public static class CompareExchangeKeys
    {
        /// <summary>
        /// Key prefix for role name reservations.
        /// Format: identity/roles/by-name/{normalizedRoleName}
        /// </summary>
        public const string RoleNamePrefix = "identity/roles/by-name/";

        /// <summary>
        /// Key prefix for username reservations.
        /// Format: identity/users/by-name/{normalizedUserName}
        /// </summary>
        public const string UserNamePrefix = "identity/users/by-name/";

        /// <summary>
        /// Key prefix for email reservations.
        /// Format: identity/emails/by-value/{normalizedEmail}
        /// </summary>
        public const string EmailPrefix = "identity/emails/by-value/";

        /// <summary>
        /// Gets the Compare Exchange key for a role name.
        /// </summary>
        /// <param name="normalizedRoleName">The normalized role name.</param>
        /// <returns>The compare exchange key.</returns>
        public static string ForRoleName(string normalizedRoleName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);
            return $"{RoleNamePrefix}{normalizedRoleName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Gets the Compare Exchange key for a username.
        /// </summary>
        /// <param name="normalizedUserName">The normalized username.</param>
        /// <returns>The compare exchange key.</returns>
        public static string ForUserName(string normalizedUserName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUserName);
            return $"{UserNamePrefix}{normalizedUserName.ToLowerInvariant()}";
        }

        /// <summary>
        /// Gets the Compare Exchange key for an email.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email.</param>
        /// <returns>The compare exchange key.</returns>
        public static string ForEmail(string normalizedEmail)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
            return $"{EmailPrefix}{normalizedEmail.ToLowerInvariant()}";
        }
    }
}
