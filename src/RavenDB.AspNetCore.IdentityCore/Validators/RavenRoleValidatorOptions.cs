using System;

namespace RavenDB.AspNetCore.IdentityCore.Validators
{
    /// <summary>
    /// Optional configuration for <see cref="RavenRoleValidator{TRole}"/> that extends Identity's built-in validation.
    /// These options work in addition to the standard RoleManager validation configured in Identity.
    /// For advanced validation (banned words, regex patterns, database-backed word lists),
    /// use the <c>RavenDB.AspNetCore.IdentityCore.Validation</c> package.
    /// </summary>
    public class RavenRoleValidatorOptions
    {
        /// <summary>
        /// Gets or sets the required minimum length for role names.
        /// If null (default), no minimum length requirement is enforced.
        /// </summary>
        public int? RequiredLength { get; set; }

        /// <summary>
        /// Fluent API: Sets the minimum required length for role names.
        /// </summary>
        /// <param name="length">The minimum length required.</param>
        /// <returns>The current options instance for method chaining.</returns>
        public RavenRoleValidatorOptions WithMinimumLength(int length)
        {
            if (length < 1)
                throw new ArgumentException("Minimum length must be at least 1.", nameof(length));

            RequiredLength = length;
            return this;
        }
    }
}
