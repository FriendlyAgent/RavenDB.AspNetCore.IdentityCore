namespace RavenDB.AspNetCore.IdentityCore.Entities.UniqueConstraints
{
    class UniqueRoleName
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UniqueRoleName"/>.
        /// </summary>
        /// <param name="value">The value that need to be unique.</param>
        public UniqueRoleName(
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
                return string.Format("UniqueRoleNames/{0}", UniqueValue);
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
