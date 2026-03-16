using System;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Base class for user contact information.
    /// </summary>
    public abstract class RavenIdentityUserContact
    {
        /// <summary>
        /// The record proving the user information is verified.
        /// </summary>
        public DateTime? ConfirmationOn { get;  set; }

        /// <summary>
        /// The record tracking when the contact information was first created.
        /// </summary>
        public DateTime? CreatedOn { get; set; }

        /// <summary>
        /// The record tracking when the contact information was last updated.
        /// </summary>
        public DateTime? UpdatedOn { get; set; }

        /// <summary>
        /// Gets a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the contact info has been confirmed, otherwise false.</value>
        public bool IsConfirmed()
        {
            return ConfirmationOn != null;
        }

        /// <summary>
        /// Sets a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the contact info has been confirmed, otherwise false.</value>
        public void SetConfirmed()
        {
            ConfirmationOn = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes the flag indicating if a user had confirmed their contact info.
        /// </summary>
        public void SetUnconfirmed()
        {
            ConfirmationOn = null;
        }
    }
}