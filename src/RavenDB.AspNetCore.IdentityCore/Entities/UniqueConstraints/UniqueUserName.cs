using System;
using System.Collections.Generic;
using System.Text;

namespace RavenDB.AspNetCore.IdentityCore.Entities.UniqueConstraints
{
    /// <summary>
    /// 
    /// </summary>
    public class UniqueUserName
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public UniqueUserName(
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
                return string.Format("UniqueUserNames/{0}", UniqueValue);
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
