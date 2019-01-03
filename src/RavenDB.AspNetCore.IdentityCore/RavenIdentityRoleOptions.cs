using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.IdentityCore.Entities;

namespace RavenDB.AspNetCore.IdentityCore
{
    public class RavenRoleOptions<TSession, TRole>
        where TSession : IAsyncDocumentSession
        where TRole : IdentityRole
    {

    }

    public class RavenIdentityRoleOptions<TRole>
        where TRole : IdentityRole
    {
        public RavenRoleOptions<IAsyncDocumentSession, TRole> RavenRoleOptions { get; set; }
            = new RavenRoleOptions<IAsyncDocumentSession, TRole>()
            {

            };
    }
}
