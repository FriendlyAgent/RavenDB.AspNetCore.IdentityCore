using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents an email address of a user.
    /// </summary>
    public class IdentityUserEmail
        : IdentityUserContact, 
        IEquatable<IdentityUserEmail>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserEmail"/>.
        /// </summary>
        public IdentityUserEmail()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserEmail"/>.
        /// </summary>
        /// <param name="email">the email address.</param>
        public IdentityUserEmail(
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
        /// Compare IdentityUserEmailAddress to see if there equals.
        /// </summary>
        /// <param name="other">The IdentityUserEmailAddress to compare.</param>
        /// <returns>True if equal.</returns>
        public bool Equals(
            IdentityUserEmail other)
        {
            return other.Email.Equals(Email);
        }
    }
}