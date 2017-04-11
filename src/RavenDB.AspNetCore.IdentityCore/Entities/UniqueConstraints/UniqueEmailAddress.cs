namespace RavenDB.AspNetCore.IdentityCore.Entities.UniqueConstraints
{
    /// <summary>
    /// The class responsible for unique email adresses.
    /// </summary>
    public class UniqueEmailAddress
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UniqueEmailAddress"/>.
        /// </summary>
        /// <param name="value">The value that need to be unique.</param>
        public UniqueEmailAddress(
            string value)
        {
            UniqueValue = value.ToLower();
        }

        /// <summary>
        /// The unique identifier.
        /// </summary>
        public string Id
        {
            get
            {
                return string.Format("UniqueEmailAddresses/{0}", UniqueValue);
            }
        }

        /// <summary>
        /// Relation with the record that wants to have a unique property.
        /// </summary>
        public string RelationId { get; set; }

        /// <summary>
        /// The unique value.
        /// </summary>
        public string UniqueValue { get; private set; }
    }
}