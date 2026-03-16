using IdentitySample.ApiEndpoints.Entities;
using RavenDB.AspNetCore.IdentityCore.Indexes;

namespace IdentitySample.ApiEndpoints.Indexes;

public class ApplicationUserIndex
    : IdentityUserIndex<ApplicationUser>
{
}
