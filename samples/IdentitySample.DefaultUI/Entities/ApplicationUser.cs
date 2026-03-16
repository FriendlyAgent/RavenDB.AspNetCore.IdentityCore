using RavenDB.AspNetCore.IdentityCore.Entities;

namespace IdentitySample.DefaultUI.Entities;

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

    public string Name { get; set; }
    public int Age { get; set; }
}
