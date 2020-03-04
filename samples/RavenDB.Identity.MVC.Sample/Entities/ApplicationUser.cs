using RavenDB.AspNetCore.IdentityCore.Entities;

namespace RavenDB.Identity.MVC.Sample.Entities
{
    public class ApplicationUser
        : RavenIdentityUser
    {
        public ApplicationUser()
        {

        }

        public ApplicationUser(
            string userName, 
            string email) 
            : base(userName, email)
        {
        }
    }
}
