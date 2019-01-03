using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace RavenDB.AspNetCore.IdentityCore
{
    public class RavenUserOptions<TSession, TUser>
        where TSession : IAsyncDocumentSession
        where TUser : IdentityUser

    {
        public Func<TSession, string, CancellationToken, Task<TUser>> GetUserByNameAsync { get; set; }

        public Func<TSession, string, CancellationToken, Task<TUser>> GetUserByEmailAsync { get; set; }

        public Func<TSession, string, string, CancellationToken, Task<TUser>> GetUserByLoginAsync { get; set; }

        public Func<TSession, string, CancellationToken, Task<List<TUser>>> GetUsersInRoleAsync { get; set; }

        public Func<TSession, Claim, CancellationToken, Task<List<TUser>>> GetUsersForClaimAsync { get; set; }
    }

    public class RavenIdentityUserOptions<TUser>
        where TUser : IdentityUser
    {
        public RavenUserOptions<IAsyncDocumentSession, TUser> RavenUserOptions { get; set; }
            = new RavenUserOptions<IAsyncDocumentSession, TUser>()
            {
                GetUserByNameAsync = delegate (
                    IAsyncDocumentSession session,
                    string normalizedUserName,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.NormalizedUserName == normalizedUserName)
                        .FirstOrDefaultAsync(cancellationToken);
                },
                GetUserByEmailAsync = delegate (
                    IAsyncDocumentSession session,
                    string normalizedEmail,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Email.NormalizedEmail == normalizedEmail)
                        .FirstOrDefaultAsync(cancellationToken);
                },
                GetUsersForClaimAsync = delegate (
                    IAsyncDocumentSession session,
                    Claim claim,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Claims.Any(
                            b => b.ClaimType == claim.Type && b.ClaimValue == claim.Value))
                        .ToListAsync();
                },
                GetUserByLoginAsync = delegate (
                    IAsyncDocumentSession session,
                    string loginProvider,
                    string providerKey,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .SingleOrDefaultAsync(
                            a => a.Logins.Any(
                                b => b.LoginProvider == loginProvider && b.ProviderKey == loginProvider), cancellationToken);
                },
                GetUsersInRoleAsync = delegate (
                    IAsyncDocumentSession session,
                    string roleId,
                    CancellationToken cancellationToken)
                {
                    return session
                        .Query<TUser>()
                        .Where(a => a.Roles.Contains(roleId))
                        .ToListAsync();
                }
            };
    }
}
