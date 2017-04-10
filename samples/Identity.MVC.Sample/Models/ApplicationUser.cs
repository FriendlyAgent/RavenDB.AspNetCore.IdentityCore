using RavenDB.AspNetCore.IdentityCore.Entities;

namespace Identity.MVC.Sample.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser
        : IdentityUser
    {
        public ApplicationUser(
            string userName, 
            string email) 
            : base(userName, email)
        {
        }
    }
}
