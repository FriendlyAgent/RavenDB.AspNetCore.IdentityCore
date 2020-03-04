using System;
using System.Security.Claims;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents a claim that a user possesses. 
    /// </summary>
    public class RavenIdentityUserClaim
        : IEquatable<RavenIdentityUserClaim>,
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
        /// Compare IdentityUserClaim to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityUserClaim to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            RavenIdentityUserClaim other)
        {
            return other.ClaimType.Equals(ClaimType) && 
                other.ClaimValue.Equals(ClaimValue);
        }

        /// <summary>
        /// Compare Claim to see if there equals.
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
        /// Converts the entity into a Claim instance.
        /// </summary>
        /// <returns>The cleam.</returns>
        public virtual Claim ToClaim()
        {
            return new Claim(ClaimType, ClaimValue);
        }
    }
}
