using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents an email address of a user.
    /// </summary>
    public class IdentityUserEmailAddress
        : IdentityUserContact, 
        IEquatable<IdentityUserEmailAddress>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserEmailAddress"/>.
        /// </summary>
        public IdentityUserEmailAddress()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserEmailAddress"/>.
        /// </summary>
        /// <param name="emailAddress">the email address.</param>
        public IdentityUserEmailAddress(
            string emailAddress)
            : this()
        {
            if (emailAddress == null)
                throw new ArgumentNullException(nameof(emailAddress));

            Email = emailAddress;
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
        /// Compare IdentityUserEmailAddress to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityUserEmailAddress to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            IdentityUserEmailAddress other)
        {
            return other.Email.Equals(Email);
        }
    }
}