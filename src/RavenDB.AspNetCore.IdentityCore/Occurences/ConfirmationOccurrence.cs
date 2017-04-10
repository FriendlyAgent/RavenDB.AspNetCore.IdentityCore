using System;

namespace RavenDB.AspNetCore.IdentityCore.Occurences
{
    /// <summary>
    /// The class that keeps track if something is confirmed.
    /// </summary>
    public class ConfirmationOccurrence
        : Occurrence
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ConfirmationOccurrence"/>.
        /// </summary>
        public ConfirmationOccurrence()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ConfirmationOccurrence"/>.
        /// </summary>
        /// <param name="confirmedOn">The date and time on which was confirmed.</param>
        public ConfirmationOccurrence(
            DateTime confirmedOn)
            : base(confirmedOn)
        {
        }
    }
}