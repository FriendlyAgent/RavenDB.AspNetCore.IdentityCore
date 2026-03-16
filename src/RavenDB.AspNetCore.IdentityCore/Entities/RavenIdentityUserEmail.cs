using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents an email address of a user.
    /// </summary>
    public class RavenIdentityUserEmail
        : RavenIdentityUserContact,
        IEquatable<RavenIdentityUserEmail>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUserEmail"/>.
        /// </summary>
        public RavenIdentityUserEmail()
            : base()
        {
            CreatedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUserEmail"/>.
        /// </summary>
        /// <param name="email">The email address.</param>
        public RavenIdentityUserEmail(
            string email)
            : this()
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }

        /// <summary>
        /// Gets or sets the contact email for this user.
        /// </summary>
        public virtual string Email { get; set; }

        /// <summary>
        /// Gets or sets the normalized email address for this user.
        /// </summary>
        public virtual string NormalizedEmail { get; set; }

        /// <summary>
        /// Compares this email with another to determine equality.
        /// </summary>
        /// <param name="other">The email to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(RavenIdentityUserEmail? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Email, other.Email, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is RavenIdentityUserEmail other && Equals(other);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return Email is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Email);
        }

        /// <summary>
        /// Determines whether two email addresses are equal.
        /// </summary>
        public static bool operator ==(RavenIdentityUserEmail? left, RavenIdentityUserEmail? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Determines whether two email addresses are not equal.
        /// </summary>
        public static bool operator !=(RavenIdentityUserEmail? left, RavenIdentityUserEmail? right)
        {
            return !Equals(left, right);
        }
    }
}