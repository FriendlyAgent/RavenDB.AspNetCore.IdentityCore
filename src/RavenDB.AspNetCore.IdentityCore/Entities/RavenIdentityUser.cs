using System;
using System.Collections.Generic;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// Represents a user in the identity system.
    /// </summary>
    public class RavenIdentityUser
        : RavenIdentityUser<RavenIdentityUserClaim, RavenIdentityUserLogin, RavenIdentityUserToken>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        public RavenIdentityUser()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <remarks>
        /// The Id property is initialized to from a new GUID string value.
        /// </remarks>
        public RavenIdentityUser(
            string userName)
            : base(userName)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <param name="email">The email</param>
        /// <remarks>
        /// The Id property is initialized to from a new GUID string value.
        /// </remarks>
        public RavenIdentityUser(
            string userName,
            string email)
            : base(userName, email)
        {
        }
    }

    /// <summary>
    /// Represents a user in the identity system
    /// </summary>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user external login.</typeparam>
    public class RavenIdentityUser<TUserClaim, TUserLogin, TUserToken>
        where TUserClaim : RavenIdentityUserClaim, new()
        where TUserLogin : RavenIdentityUserLogin, new()
        where TUserToken : RavenIdentityUserToken, new()
    { 
        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        public RavenIdentityUser()
        {
            CreatedOn = DateTime.UtcNow;
            ConcurrencyStamp = Guid.NewGuid().ToString();
            Roles = new List<string>();
            Claims = new List<TUserClaim>();
            Logins = new List<TUserLogin>();
            Tokens = new List<TUserToken>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        /// <param name="userName">The user name.</param>
        public RavenIdentityUser(
            string userName)
            : this()
        {
            UserName = userName;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RavenIdentityUser"/>.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="email"></param>
        public RavenIdentityUser(
            string userName, 
            string email)
            : this(userName)
        {
            if (email != null)
                Email = new RavenIdentityUserEmail(email);
        }

        /// <summary>
        /// Gets or sets the primary key for this user.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the user name for this user.
        /// </summary>
        public virtual string UserName { get; set; }

        /// <summary>
        /// Gets or sets the normalized user name for this user.
        /// </summary>
        public virtual string NormalizedUserName { get; set; }

        /// <summary>
        /// Gets or sets a salted and hashed representation of the password for this user.
        /// </summary>
        public virtual string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if two factor authentication is enabled for this user.
        /// </summary>
        /// <value>True if 2fa is enabled, otherwise false.</value>
        public virtual bool IsTwoFactorEnabled { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if the user could be locked out.
        /// </summary>
        /// <value>True if the user could be locked out, otherwise false.</value>

        public virtual bool IsLockoutEnabled { get; set; }

        /// <summary>
        /// Gets or sets the number of failed login attempts for the current user.
        /// </summary>
        public virtual int AccessFailedCount { get; set; }

        /// <summary>
        /// Gets or sets the moment a user got created.
        /// </summary>
        public virtual DateTime? CreatedOn { get; private set; }

        /// <summary>
        /// Gets or sets the date and time, in UTC, when any user lockout ends.
        /// </summary>
        /// <remarks>
        /// A value in the past means the user is not locked out.
        /// </remarks>
        public virtual DateTime? LockoutEndDate { get; set; }

        /// <summary>
        /// Gets or sets the email address for this user.
        /// </summary>
        public virtual RavenIdentityUserEmail Email { get; set; }

        /// <summary>
        /// Gets the email address for this user.
        /// </summary>
        public string GetEmail()
        {
            return Email?.Email;
        }

        /// <summary>
        /// Gets a flag indicating if a user has confirmed their mail address.
        /// </summary>
        /// <value>True if the mail address has been confirmed, otherwise false.</value>
        public bool IsEmailConfirmed()
        {
            if (Email == null)
                return false;

            return Email.IsConfirmed();
        }

        /// <summary>
        /// Gets or sets a telephone number for the user.
        /// </summary>
        public virtual RavenIdentityUserPhoneNumber PhoneNumber { get; set; }

        /// <summary>
        /// Gets a telephone number for the user.
        /// </summary>
        public string GetPhoneNumber()
        {
            return PhoneNumber?.Number;
        }

        /// <summary>
        /// Gets a flag indicating if a user has confirmed their phone number.
        /// </summary>
        /// <value>True if the phone number has been confirmed, otherwise false.</value>
        public bool IsPhoneNumberConfirmed()
        {
            if (PhoneNumber == null)
                return false;

            return PhoneNumber.IsConfirmed();
        }

        /// <summary>
        /// A random value that must change whenever a users credentials change (password changed, login removed)
        /// </summary>
        public string SecurityStamp { get; set; }

        /// <summary>
        /// A random value that must change whenever a user is persisted to the store
        /// </summary>
        public virtual string ConcurrencyStamp { get; set; }

        /// <summary>
        /// Navigation property for the roles this user belongs to.
        /// </summary>
        public virtual List<string> Roles { get; set; }

        /// <summary>
        /// Navigation property for the claims this user possesses.
        /// </summary>
        public virtual List<TUserClaim> Claims { get; set; }

        /// <summary>
        /// Navigation property for this users login accounts.
        /// </summary>
        public virtual List<TUserLogin> Logins { get; set; }

        /// <summary>
        /// Navigation property for this users tokens.
        /// </summary>
        public virtual List<TUserToken> Tokens { get; set; }

        /// <summary>
        /// Returns the username for this user.
        /// </summary>
        public override string ToString()
        {
            return UserName;
        }
    }
}