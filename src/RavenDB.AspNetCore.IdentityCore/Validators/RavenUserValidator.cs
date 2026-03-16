using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Validators
{
    /// <summary>
    /// Used to override the user validation because ravendb cannot check for uniqueness on this level.
    /// Extends Identity's built-in validation with optional minimum length checking.
    /// For advanced validation (banned words, regex patterns, database-backed word lists),
    /// use the <c>RavenDB.AspNetCore.IdentityCore.Validation</c> package.
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public class RavenUserValidator<TUser>
        : UserValidator<TUser>
        where TUser : RavenIdentityUser
    {
        private readonly RavenUserValidatorOptions _validatorOptions;

        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserValidator{TUser}"/>.
        /// </summary>
        /// <param name="errors">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        /// <param name="validatorOptions">Optional validator configuration for username length requirements.</param>
        public RavenUserValidator(
            IdentityErrorDescriber errors = null,
            IOptions<RavenUserValidatorOptions> validatorOptions = null)
            : base(errors)
        {
            _validatorOptions = validatorOptions?.Value;
        }

        /// <summary>
        /// Validates the specified <paramref name="user"/> as an asynchronous operation.
        /// Performs all standard Identity validation except duplicate username/email checks
        /// (duplicates are handled by RavenDB Compare-Exchange at the store level).
        /// </summary>
        /// <param name="manager">The <see cref="UserManager{TUser}"/> that can be used to retrieve user properties.</param>
        /// <param name="user">The user to validate.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the validation operation.</returns>
        public override async Task<IdentityResult> ValidateAsync(
            UserManager<TUser> manager,
            TUser user)
        {
            ArgumentNullException.ThrowIfNull(manager);
            ArgumentNullException.ThrowIfNull(user);

            var errors = await ValidateUserName(manager, user);
            if (manager.Options.User.RequireUniqueEmail)
            {
                errors = await ValidateEmail(manager, user, errors);
            }

            return errors?.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        private async Task<List<IdentityError>> ValidateUserName(
            UserManager<TUser> manager,
            TUser user)
        {
            List<IdentityError> errors = null;
            var userName = await manager.GetUserNameAsync(user);

            // Check if username is null or whitespace
            if (string.IsNullOrWhiteSpace(userName))
            {
                errors ??= new List<IdentityError>();
                errors.Add(Describer.InvalidUserName(userName));
                return errors;
            }

            // Check allowed characters (same as base UserValidator)
            if (!string.IsNullOrEmpty(manager.Options.User.AllowedUserNameCharacters) &&
                userName.Any(c => !manager.Options.User.AllowedUserNameCharacters.Contains(c)))
            {
                errors ??= new List<IdentityError>();
                errors.Add(Describer.InvalidUserName(userName));
                return errors;
            }

            // Check minimum length
            if (_validatorOptions != null &&
                _validatorOptions.RequiredLength.HasValue &&
                userName.Length < _validatorOptions.RequiredLength.Value)
            {
                errors ??= new List<IdentityError>();
                errors.Add(new IdentityError
                {
                    Code = "UserNameTooShort",
                    Description = $"Username must be at least {_validatorOptions.RequiredLength.Value} characters."
                });
            }

            // NOTE: Duplicate username check is NOT done here - it's handled by
            // RavenDB Compare-Exchange in the store layer for proper distributed uniqueness

            return errors;
        }

        private async Task<List<IdentityError>> ValidateEmail(
            UserManager<TUser> manager,
            TUser user,
            List<IdentityError> errors)
        {
            var email = await manager.GetEmailAsync(user);

            // Check if email is null or whitespace
            if (string.IsNullOrWhiteSpace(email))
            {
                errors ??= new List<IdentityError>();
                errors.Add(Describer.InvalidEmail(email));
                return errors;
            }

            // Validate email format using EmailAddressAttribute (same as base UserValidator)
            if (!new EmailAddressAttribute().IsValid(email))
            {
                errors ??= new List<IdentityError>();
                errors.Add(Describer.InvalidEmail(email));
                return errors;
            }

            // NOTE: Duplicate email check is NOT done here - it's handled by
            // RavenDB Compare-Exchange in the store layer for proper distributed uniqueness

            return errors;
        }
    }
}
