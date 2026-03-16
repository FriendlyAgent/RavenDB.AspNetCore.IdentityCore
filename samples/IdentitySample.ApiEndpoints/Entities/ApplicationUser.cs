using RavenDB.AspNetCore.IdentityCore.Entities;

namespace IdentitySample.ApiEndpoints.Entities;

public class ApplicationUser : RavenIdentityUser
{
    public ApplicationUser()
    {
    }

    public ApplicationUser(string userName)
        : base(userName)
    {
    }

    public ApplicationUser(string userName, string email)
        : base(userName, email)
    {
    }
}
