using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;

namespace RavenDB.AspNetCore.IdentityCore
{
    /// <summary>
    /// Represents all the user options you can use to configure the identity system.
    /// </summary>
    public class RavenIdentityUserOptions
        : RavenIdentityUserOptions<RavenIdentityUser>
    {
    }

    /// <summary>
    /// Represents all the user options you can use to configure the identity system.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class RavenIdentityUserOptions<TUser>
        : RavenIdentityUserOptions<TUser, IAsyncDocumentSession>
        where TUser : RavenIdentityUser
    {
    }

    /// <summary>
    /// Represents all the user options you can use to configure the identity system.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TSession">The type of the data context class used to access the session.</typeparam>
    public class RavenIdentityUserOptions<TUser, TSession>
        where TUser : RavenIdentityUser
        where TSession : IAsyncDocumentSession
    {
        /// <summary>
        /// Gets or sets a flag indicating if static indexes should be used.
        /// </summary>
        /// <remarks>
        /// Indexes need to be deployed to the server in order for static index queries to work.
        /// </remarks>
        /// <value>
        /// True if static indexes should be used, otherwise false.
        /// </value>
        /// <seealso cref="Indexes.IdentityUserIndex{TUser}"/>
        public bool UseStaticIndexes { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if changes should be persisted after CreateAsync, UpdateAsync and DeleteAsync are called.
        /// </summary>
        /// <value>
        /// True if changes should be automatically persisted, otherwise false.
        /// </value>
        public bool AutoSaveChanges { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag indicating if username and email uniqueness should be enforced using Compare/Exchange operations.
        /// </summary>
        /// <value>
        /// True if uniqueness constraints should be enforced (default), false to disable.
        /// </value>
        /// <remarks>
        /// <para>
        /// <strong>⚠️ WARNING:</strong> Disabling this option turns off atomic Compare/Exchange operations
        /// that ensure username and email uniqueness across the system.
        /// </para>
        /// <para>
        /// <strong>When disabled:</strong>
        /// <list type="bullet">
        /// <item><description>Multiple users can be created with the same username</description></item>
        /// <item><description>Multiple users can be created with the same email address</description></item>
        /// <item><description>Race conditions may occur during concurrent user creation</description></item>
        /// <item><description>YOU must implement your own uniqueness validation logic</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Use cases for disabling:</strong>
        /// <list type="bullet">
        /// <item><description>Single-tenant systems where uniqueness is guaranteed by external constraints</description></item>
        /// <item><description>Systems where usernames/emails don't need to be unique</description></item>
        /// <item><description>Custom uniqueness validation is handled at a higher level</description></item>
        /// <item><description>Performance-critical scenarios where Compare/Exchange overhead is unacceptable</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Default:</strong> true (uniqueness is enforced)
        /// </para>
        /// </remarks>
        public bool EnforceUniqueConstraints { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of retry attempts when releasing a Compare/Exchange reservation fails due to concurrency.
        /// </summary>
        /// <value>
        /// The number of retries. Set to 0 to disable retries. Defaults to 1.
        /// </value>
        /// <remarks>
        /// A release can fail when the Compare/Exchange index changes between the Get and Delete operations.
        /// Retrying with a fresh index typically resolves this. If all retries are exhausted, the reservation
        /// becomes orphaned and the username/email cannot be reused until manually cleaned up.
        /// </remarks>
        public int ReservationReleaseRetryCount { get; set; } = 1;
    }
}
