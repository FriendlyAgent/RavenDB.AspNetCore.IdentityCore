using System;

namespace RavenDB.AspNetCore.IdentityCore.Occurences
{
    /// <summary>
    /// The class that keeps track of when something will happen.
    /// </summary>
    public class FutureOccurrence
        : Occurrence
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FutureOccurrence"/>.
        /// </summary>
        public FutureOccurrence()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FutureOccurrence"/>.
        /// </summary>
        /// <param name="willOccurOn">TThe date and time when something will happen.</param>
        public FutureOccurrence(
            DateTime willOccurOn)
            : base(willOccurOn)
        {
        }
    }
}