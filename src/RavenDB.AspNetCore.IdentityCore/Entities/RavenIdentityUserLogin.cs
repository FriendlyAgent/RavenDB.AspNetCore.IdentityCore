using Microsoft.AspNetCore.Identity;
using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents a login and its associated provider for a user.
    /// </summary>
    public class RavenIdentityUserLogin
        : IEquatable<RavenIdentityUserLogin>,
        IEquatable<UserLoginInfo>
    {
        /// <summary>
        /// Gets or sets the login provider for the login (e.g. facebook, google).
        /// </summary>
        public virtual string LoginProvider { get; set; }

        /// <summary>
        /// Gets or sets the unique provider identifier for this login.
        /// </summary>
        public virtual string ProviderKey { get; set; }

        /// <summary>
        /// Gets or sets the friendly name used in a UI for this login.
        /// </summary>
        public virtual string ProviderDisplayName { get; set; }

        /// <summary>
        /// Gets or sets when the login was created.
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// Converts the entity into a UserLoginInfo instance.
        /// </summary>
        /// <returns>The UserLoginInfo.</returns>
        public virtual UserLoginInfo ToUserLoginInfo()
        {
            return new UserLoginInfo(LoginProvider, ProviderKey, ProviderDisplayName);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is RavenIdentityUserLogin other && Equals(other)
                || obj is UserLoginInfo info && Equals(info);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(LoginProvider ?? string.Empty),
                StringComparer.Ordinal.GetHashCode(ProviderKey ?? string.Empty));
        }

        /// <summary>
        /// Compares this login with another to determine equality.
        /// </summary>
        /// <param name="other">The login to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            RavenIdentityUserLogin other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(other.LoginProvider, LoginProvider, StringComparison.Ordinal)
                && string.Equals(other.ProviderKey, ProviderKey, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares this login with a <see cref="UserLoginInfo"/> to determine equality.
        /// </summary>
        /// <param name="other">The <see cref="UserLoginInfo"/> to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            UserLoginInfo other)
        {
            if (other is null) return false;

            return string.Equals(other.LoginProvider, LoginProvider, StringComparison.Ordinal)
                && string.Equals(other.ProviderKey, ProviderKey, StringComparison.Ordinal);
        }
    }
}