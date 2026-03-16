using RavenDB.AspNetCore.IdentityCore.Entities;

namespace IdentitySample.Entities;

// Add profile data for application users by adding properties to the ApplicationUser class
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
