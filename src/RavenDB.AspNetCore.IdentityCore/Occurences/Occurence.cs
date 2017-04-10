using System;

namespace RavenDB.AspNetCore.IdentityCore.Occurences
{
    /// <summary>
    /// The class with all occurrences implement.
    /// </summary>
    public abstract class Occurrence
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Occurrence"/>.
        /// </summary>
        public Occurrence()
            : this(DateTime.UtcNow)
        {

        }

        /// <summary>
        /// Initializes a new instance of <see cref="Occurrence"/>.
        /// </summary>
        /// <param name="occurOn">The time and date on which the event took place.</param>
        public Occurrence(DateTime occurOn)
        {
            OccurredOn = occurOn;
        }

        /// <summary>
        /// The time and date on which the event took place.
        /// </summary>
        public DateTime OccurredOn { get; private set; }
    }
}
