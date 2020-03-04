using System;
using System.Collections.Generic;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// The default implementation of <see cref="RavenIdentityRole"/>
    /// </summary>
    public class RavenIdentityRole
        : RavenIdentityRole<RavenIdentityRoleClaim>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityRole"/>.
        /// </summary>
        public RavenIdentityRole()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityRole"/>.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        public RavenIdentityRole(string roleName)
            : base(roleName)
        {
        }
    }

    /// <summary>
    /// Represents a role in the identity system
    /// </summary>
    /// <typeparam name="TRoleClaim">The type used for role claims.</typeparam>
    public class RavenIdentityRole<TRoleClaim>
      where TRoleClaim : RavenIdentityRoleClaim, new()
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityRole"/>.
        /// </summary>
        public RavenIdentityRole()
        {
            CreatedOn = DateTime.UtcNow;
            ConcurrencyStamp = Guid.NewGuid().ToString();
            Claims = new List<TRoleClaim>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityRole"/>.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        public RavenIdentityRole(string roleName)
            : this()
        {
            RoleName = roleName;
        }

        /// <summary>
        /// Gets or sets the primary key for this role.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the moment a role got created.
        /// </summary>
        public virtual DateTime? CreatedOn { get; private set; }

        /// <summary>
        /// Navigation property for claims in this role.
        /// </summary>
        public virtual ICollection<TRoleClaim> Claims { get; set; }

        /// <summary>
        /// Gets or sets the name for this role.
        /// </summary>
        public virtual string RoleName { get; set; }

        /// <summary>
        /// Gets or sets the normalized name for this role.
        /// </summary>
        public virtual string NormalizedRoleName { get; set; }

        /// <summary>
        /// A random value that should change whenever a role is persisted to the store
        /// </summary>
        public virtual string ConcurrencyStamp { get; set; }

        /// <summary>
        /// Returns the name of the role.
        /// </summary>
        /// <returns>The name of the role.</returns>
        public override string ToString()
        {
            return RoleName;
        }
    }
}