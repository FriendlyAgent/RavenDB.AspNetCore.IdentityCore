using Microsoft.AspNetCore.Identity;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore.Validators
{
    /// <summary>
    /// Used to override the user validation because ravendb cannot check for uniqueness on this level.
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public class RavenUserValidator<TUser>
        : UserValidator<TUser>
        where TUser : RavenIdentityUser
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RavenUserValidator{TUser}"/>.
        /// </summary>
        /// <param name="errors">The <see cref="IdentityErrorDescriber"/> used to provider error messages.</param>
        public RavenUserValidator(
            IdentityErrorDescriber errors = null)
            : base(errors)
        {

        }

        /// <summary>
        /// Validates the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="manager">The <see cref="UserManager{TUser}"/> that can be used to retrieve user properties.</param>
        /// <param name="user">The user to validate.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the validation operation.</returns>
        public override async Task<IdentityResult> ValidateAsync(
            UserManager<TUser> manager,
            TUser user)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var errors = new List<IdentityError>();

            await ValidateUserName(manager, user, errors);
            if (manager.Options.User.RequireUniqueEmail)
            {
                await ValidateEmail(manager, user, errors);
            }

            return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
        }

        private async Task ValidateUserName(
            UserManager<TUser> manager,
            TUser user,
            ICollection<IdentityError> errors)
        {
            var userName = await manager.GetUserNameAsync(user);
            if (string.IsNullOrWhiteSpace(userName))
            {
                errors.Add(Describer.InvalidUserName(userName));
                return;
            }
        }

        private async Task ValidateEmail(
            UserManager<TUser> manager,
            TUser user,
            List<IdentityError> errors)
        {
            var email = await manager.GetEmailAsync(user);
            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add(Describer.InvalidEmail(email));
                return;
            }
        }
    }
}