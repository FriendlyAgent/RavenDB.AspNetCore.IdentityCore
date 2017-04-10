using Microsoft.AspNetCore.Identity;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Validators
{
    /// <summary>
    /// Used to override the role validation because ravendb cannot check for uniqueness on this level.
    /// </summary>
    /// <typeparam name="TRole"></typeparam>
    public class RavenRoleValidator<TRole>
        : RoleValidator<TRole>
        where TRole : IdentityRole
    {
        /// <summary>
        /// Gets the <see cref="IdentityErrorDescriber"/> used to provider error messages for the current <see cref="RavenRoleValidator{TRole}"/>.
        /// </summary>
        /// <value>Yhe <see cref="IdentityErrorDescriber"/> used to provider error messages for the current <see cref="RavenRoleValidator{TRole}"/>.</value>
        public IdentityErrorDescriber Describer { get; private set; }

        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleValidator{TRole}"/>.
        /// </summary>
        /// <param name="errors">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        public RavenRoleValidator(IdentityErrorDescriber errors = null)
            : base(errors)
        {
            Describer = errors ?? new IdentityErrorDescriber();
        }

        /// <summary>
        /// Validates a role as an asynchronous operation.
        /// </summary>
        /// <param name="manager">The <see cref="RoleManager{TRole}"/> managing the role store.</param>
        /// <param name="role">The role to validate.</param>
        /// <returns>A <see cref="Task"/> that represents the <see cref="IdentityResult"/> of the asynchronous validation.</returns>
        public override async Task<IdentityResult> ValidateAsync(
            RoleManager<TRole> manager, 
            TRole role)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            var errors = new List<IdentityError>();

            await ValidateRoleName(manager, role, errors);

            return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        private async Task ValidateRoleName(RoleManager<TRole> manager, TRole role,
          ICollection<IdentityError> errors)
        {
            var roleName = await manager.GetRoleNameAsync(role);
            if (string.IsNullOrWhiteSpace(roleName))
            {
                errors.Add(Describer.InvalidRoleName(roleName));
            }
        }
    }
}