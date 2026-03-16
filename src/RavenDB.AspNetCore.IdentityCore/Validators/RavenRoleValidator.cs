using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Validators
{
    /// <summary>
    /// Used to override the role validation because ravendb cannot check for uniqueness on this level.
    /// Extends Identity's built-in validation with optional minimum length checking.
    /// For advanced validation (banned words, regex patterns, database-backed word lists),
    /// use the <c>RavenDB.AspNetCore.IdentityCore.Validation</c> package.
    /// </summary>
    /// <typeparam name="TRole"></typeparam>
    public class RavenRoleValidator<TRole>
        : RoleValidator<TRole>
        where TRole : RavenIdentityRole
    {
        private readonly RavenRoleValidatorOptions _validatorOptions;

        /// <summary>
        /// Gets the <see cref="IdentityErrorDescriber"/> used to provider error messages for the current <see cref="RavenRoleValidator{TRole}"/>.
        /// </summary>
        /// <value>The <see cref="IdentityErrorDescriber"/> used to provider error messages for the current <see cref="RavenRoleValidator{TRole}"/>.</value>
        public IdentityErrorDescriber Describer { get; private set; }

        /// <summary>
        /// Constructs a new instance of <see cref="RavenRoleValidator{TRole}"/>.
        /// </summary>
        /// <param name="errors">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="validatorOptions">Optional validator configuration for role name length requirements.</param>
        public RavenRoleValidator(
            IdentityErrorDescriber errors = null,
            IOptions<RavenRoleValidatorOptions> validatorOptions = null)
            : base(errors)
        {
            Describer = errors ?? new IdentityErrorDescriber();
            _validatorOptions = validatorOptions?.Value;
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
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(role);
            var errors = new List<IdentityError>();

            await ValidateRoleName(manager, role, errors);

            return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        private async Task ValidateRoleName(
            RoleManager<TRole> manager,
            TRole role,
            ICollection<IdentityError> errors)
        {
            var roleName = await manager.GetRoleNameAsync(role);
            if (string.IsNullOrWhiteSpace(roleName))
            {
                errors.Add(Describer.InvalidRoleName(roleName));
                return;
            }

            // Check minimum length
            if (_validatorOptions != null &&
                _validatorOptions.RequiredLength.HasValue &&
                roleName.Length < _validatorOptions.RequiredLength.Value)
            {
                errors.Add(new IdentityError
                {
                    Code = "RoleNameTooShort",
                    Description = $"Role name must be at least {_validatorOptions.RequiredLength.Value} characters."
                });
            }
        }
    }
}
