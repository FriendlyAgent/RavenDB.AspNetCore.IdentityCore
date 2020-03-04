using System;
using System.Security.Claims;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents a claim that is granted to all users within a role.
    /// </summary>
    public class RavenIdentityRoleClaim
        : IEquatable<RavenIdentityRoleClaim>,
        IEquatable<Claim>
    {
        /// <summary>
        /// Gets or sets the claim type for this claim.
        /// </summary>
        public virtual string ClaimType { get; set; }

        /// <summary>
        /// Gets or sets the claim value for this claim.
        /// </summary>
        public virtual string ClaimValue { get; set; }

        /// <summary>
        /// Compare Claims to see if there equals. 
        /// </summary>
        /// <param name="other">The Claim to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            Claim other)
        {
            return other.Type.Equals(ClaimType) &&
                other.Value.Equals(ClaimValue);
        }

        /// <summary>
        /// Compare IdentityRoleClaims to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityRoleClaims to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            RavenIdentityRoleClaim other)
        {
            return other.ClaimType.Equals(ClaimType) &&
                other.ClaimValue.Equals(ClaimValue);
        }

        /// <summary>
        /// Constructs a new claim with the type and value.
        /// </summary>
        /// <returns>A new instance of <see cref="Claim"/>.</returns>
        public virtual Claim ToClaim()
        {
            return new Claim(ClaimType, ClaimValue);
        }
    }
}
