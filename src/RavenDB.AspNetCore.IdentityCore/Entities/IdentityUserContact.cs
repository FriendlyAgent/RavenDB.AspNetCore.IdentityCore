using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// The class with all  contact information implement.
    /// </summary>
    public abstract class IdentityUserContact
    {
        /// <summary>
        /// The record proving the user information is verified.
        /// </summary>
        public DateTime? ConfirmationOn { get;  set; }

        /// <summary>
        /// Gets  a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the email address has been confirmed, otherwise false.</value>
        public bool IsConfirmed()
        {
            return ConfirmationOn != null;
        }

        /// <summary>
        /// Sets a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the email address has been confirmed, otherwise false.</value>
        public void SetConfirmed()
        {
            ConfirmationOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Remove the flag indicating if a user had confirmed their contact info.
        /// </summary>
        public void SetUnconfirmed()
        {
            ConfirmationOn = null;
        }
    }
}