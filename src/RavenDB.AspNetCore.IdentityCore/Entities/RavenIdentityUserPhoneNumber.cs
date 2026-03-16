using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents a phone number of a user.
    /// </summary>
    public class RavenIdentityUserPhoneNumber
        : RavenIdentityUserContact,
        IEquatable<RavenIdentityUserPhoneNumber>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUserPhoneNumber"/>.
        /// </summary>
        public RavenIdentityUserPhoneNumber()
            : base()
        {
            CreatedOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUserPhoneNumber"/>.
        /// </summary>
        /// <param name="phoneNumber">The phone number.</param>
        public RavenIdentityUserPhoneNumber(
            string phoneNumber)
           : this()
        {
            Number = phoneNumber ?? throw new ArgumentNullException(nameof(phoneNumber));
        }

        /// <summary>
        /// Gets or sets the contact number for this user.
        /// </summary>
        public virtual string Number { get; set; }

        /// <summary>
        /// Compares this phone number with another to determine equality.
        /// </summary>
        /// <param name="other">The phone number to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(RavenIdentityUserPhoneNumber? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Number, other.Number, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is RavenIdentityUserPhoneNumber other && Equals(other);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return Number?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Determines whether two phone numbers are equal.
        /// </summary>
        public static bool operator ==(RavenIdentityUserPhoneNumber? left, RavenIdentityUserPhoneNumber? right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Determines whether two phone numbers are not equal.
        /// </summary>
        public static bool operator !=(RavenIdentityUserPhoneNumber? left, RavenIdentityUserPhoneNumber? right)
        {
            return !Equals(left, right);
        }
    }
}