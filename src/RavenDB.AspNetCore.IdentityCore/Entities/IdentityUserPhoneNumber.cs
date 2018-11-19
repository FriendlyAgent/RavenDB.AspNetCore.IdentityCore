using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents an phone number of a user.
    /// </summary>
    public class IdentityUserPhoneNumber
        : IdentityUserContact,
        IEquatable<IdentityUserPhoneNumber>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserPhoneNumber"/>.
        /// </summary>
        public IdentityUserPhoneNumber()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserPhoneNumber"/>.
        /// </summary>
        /// <param name="phoneNumber">the phone number.</param>
        public IdentityUserPhoneNumber(
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
        /// Compare IdentityUserPhoneNumber to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityUserPhoneNumber to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            IdentityUserPhoneNumber other)
        {
            return other.Number.Equals(Number);
        }
    }
}