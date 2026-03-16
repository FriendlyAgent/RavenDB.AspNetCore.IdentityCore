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
        /// Gets or sets when the claim was created.
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets when the claim was last updated.
        /// </summary>
        public DateTime? UpdatedOn { get; set; }

        /// <summary>
        /// Compares this claim with another to determine equality.
        /// </summary>
        /// <param name="other">The claim to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            RavenIdentityUserClaim other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(other.ClaimType, ClaimType, StringComparison.Ordinal) &&
                string.Equals(other.ClaimValue, ClaimValue, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares this claim with a <see cref="Claim"/> to determine equality.
        /// </summary>
        /// <param name="other">The <see cref="Claim"/> to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            Claim other)
        {
            if (other is null) return false;

            return string.Equals(other.Type, ClaimType, StringComparison.Ordinal) &&
                string.Equals(other.Value, ClaimValue, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is RavenIdentityUserClaim other && Equals(other)
                || obj is Claim claim && Equals(claim);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(ClaimType ?? string.Empty),
                StringComparer.Ordinal.GetHashCode(ClaimValue ?? string.Empty));
        }

        /// <summary>
        /// Converts the entity into a Claim instance.
        /// </summary>
        /// <returns>The claim.</returns>
        public virtual Claim ToClaim()
        {
            return new Claim(ClaimType, ClaimValue);
        }
    }
}
