using System;

namespace RavenDB.AspNetCore.IdentityCore.Occurences
{
    /// <summary>
    /// The class that keeps track when something was created.
    /// </summary>
    public class CreationOccurrence
        : Occurrence
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CreationOccurrence"/>.
        /// </summary>
        public CreationOccurrence()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="CreationOccurrence"/>.
        /// </summary>
        /// <param name="createdOn">The date and time on which something was created.</param>
        public CreationOccurrence(
            DateTime createdOn)
            : base(createdOn)
        {
        }
    }
}