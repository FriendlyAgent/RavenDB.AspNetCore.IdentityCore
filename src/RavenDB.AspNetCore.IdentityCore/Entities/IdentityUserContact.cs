using RavenDB.AspNetCore.IdentityCore.Occurences;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// The class with all  contact information implement.
    /// </summary>
    public abstract class IdentityUserContact
    {
        /// <summary>
        /// Initializes a new instance of <see cref="IdentityUserContact"/>.
        /// </summary>
        public IdentityUserContact()
        {
        }

        /// <summary>
        /// The record proving the user information is verified.
        /// </summary>
        public ConfirmationOccurrence ConfirmationRecord { get; set; }

        /// <summary>
        /// Gets  a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the email address has been confirmed, otherwise false.</value>
        public bool IsConfirmed()
        {
            return ConfirmationRecord != null;
        }

        /// <summary>
        /// Sets a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <value>True if the email address has been confirmed, otherwise false.</value>
        public void SetConfirmed()
        {
            SetConfirmed(new ConfirmationOccurrence());
        }

        /// <summary>
        /// Sets a flag indicating if a user has confirmed their contact info.
        /// </summary>
        /// <param name="confirmationRecord">Contains the date and time when the conformation has occurred.</param>
        public void SetConfirmed(
            ConfirmationOccurrence confirmationRecord)
        {
            if (ConfirmationRecord == null)
                ConfirmationRecord = confirmationRecord;
        }

        /// <summary>
        /// Remove the flag indicating if a user had confirmed their contact info.
        /// </summary>
        public void SetUnconfirmed()
        {
            ConfirmationRecord = null;
        }
    }
}