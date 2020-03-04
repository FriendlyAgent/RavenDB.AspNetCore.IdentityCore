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
        /// Converts the entity into a UserLoginInfo instance.
        /// </summary>
        /// <returns>The UserLoginInfo.</returns>
        public virtual UserLoginInfo ToUserLoginInfo()
        {
            return new UserLoginInfo(LoginProvider, ProviderKey, ProviderDisplayName);
        }

        /// <summary>
        /// Compare IdentityUserLogin to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityUserLogin to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            RavenIdentityUserLogin other)
        {
            return other.LoginProvider.Equals(LoginProvider)
                && other.ProviderKey.Equals(ProviderKey);
        }
        /// <summary>
        /// Compare UserLoginInfo to see if there equals.
        /// </summary>
        /// <param name="other">The UserLoginInfo to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            UserLoginInfo other)
        {
            return other.LoginProvider.Equals(LoginProvider)
                && other.ProviderKey.Equals(ProviderKey);
        }
    }
}